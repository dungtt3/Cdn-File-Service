using System.Security.Cryptography;
using CdnFileService.Application.Common;
using CdnFileService.Application.DTOs;
using CdnFileService.Application.Interfaces;
using CdnFileService.Domain.Entities;
using CdnFileService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CdnFileService.Infrastructure.Services;

/// <summary>
/// Core storage pipeline: validation, SHA-256 hashing, duplicate detection, versioning,
/// image-variant generation and CDN URL building. Assumes <see cref="StorageOptions.RootPath"/>
/// has been resolved to an absolute path at startup.
/// </summary>
public class FileStorageService : IFileStorageService
{
    private readonly AppDbContext _db;
    private readonly IFileValidator _validator;
    private readonly IImageProcessor _imageProcessor;
    private readonly StorageOptions _options;
    private readonly ILogger<FileStorageService> _logger;

    public FileStorageService(
        AppDbContext db,
        IFileValidator validator,
        IImageProcessor imageProcessor,
        IOptions<StorageOptions> options,
        ILogger<FileStorageService> logger)
    {
        _db = db;
        _validator = validator;
        _imageProcessor = imageProcessor;
        _options = options.Value;
        _logger = logger;
    }

    private string Root => _options.RootPath;

    public string BuildCdnUrl(string relativePath)
    {
        var rel = relativePath.Replace('\\', '/').TrimStart('/');
        return $"{_options.CdnBaseUrl.TrimEnd('/')}/{rel}";
    }

    public async Task<UploadResultDto> UploadAsync(UploadRequest request, CancellationToken cancellationToken = default)
    {
        var fileName = SanitizeFileName(request.OriginalFileName);
        if (string.IsNullOrEmpty(fileName))
            return UploadResultDto.Failed("Invalid file name.");

        // 1. Stream to a temp file while hashing and enforcing the size cap.
        string tempPath;
        long size;
        string hash;
        try
        {
            (tempPath, size, hash) = await WriteTempAsync(request.Content, cancellationToken);
        }
        catch (UploadTooLargeException)
        {
            return UploadResultDto.Failed(
                $"File exceeds the maximum size of {_options.MaxUploadSizeBytes / (1024 * 1024)} MB.");
        }

        try
        {
            // 2. Validate (extension whitelist/blacklist, size, MIME).
            var validation = _validator.Validate(fileName, size, request.ContentType);
            if (!validation.IsValid)
                return UploadResultDto.Failed(validation.Error!);

            var relativeTarget = BuildRelativePath(request.Folder, request.SubPath, fileName);
            var absoluteTarget = ResolveWithinRoot(relativeTarget);
            if (absoluteTarget is null)
                return UploadResultDto.Failed("Invalid target path.");

            var existingByPath = await _db.Files
                .FirstOrDefaultAsync(f => f.RelativePath == relativeTarget && !f.IsDeleted, cancellationToken);

            if (existingByPath is not null)
                return await HandleExistingPathAsync(existingByPath, tempPath, fileName, size, hash, request, cancellationToken);

            // 3. New logical path. If the same content already exists, reuse the bytes (dedupe).
            var existingByHash = await _db.Files
                .FirstOrDefaultAsync(f => f.Hash == hash && !f.IsDeleted, cancellationToken);

            if (existingByHash is not null)
            {
                var aliasAsset = NewAsset(fileName, request.OriginalFileName, size, hash, request.UserName,
                    existingByHash.PhysicalPath, existingByHash.RelativePath, existingByHash.Folder);
                _db.Files.Add(aliasAsset);
                await _db.SaveChangesAsync(cancellationToken);
                return Ok(aliasAsset, wasDuplicate: true);
            }

            // 4. Brand-new file: move temp bytes into place.
            Directory.CreateDirectory(Path.GetDirectoryName(absoluteTarget)!);
            MoveOverwrite(tempPath, absoluteTarget);
            tempPath = string.Empty; // consumed

            var asset = NewAsset(fileName, request.OriginalFileName, size, hash, request.UserName,
                absoluteTarget, relativeTarget, request.Folder);
            _db.Files.Add(asset);

            // Initial version record.
            asset.Versions.Add(new FileVersion
            {
                VersionNumber = 1,
                PhysicalPath = absoluteTarget,
                Hash = hash,
                Size = size,
                CreatedBy = request.UserName,
                CreatedDate = DateTime.UtcNow,
                IsCurrent = true
            });

            await _db.SaveChangesAsync(cancellationToken);
            await TryGenerateImageVariantsAsync(absoluteTarget, fileName, cancellationToken);

            return Ok(asset, wasDuplicate: false);
        }
        finally
        {
            SafeDelete(tempPath);
        }
    }

    private async Task<UploadResultDto> HandleExistingPathAsync(FileAsset asset, string tempPath, string fileName,
        long size, string hash, UploadRequest request, CancellationToken cancellationToken)
    {
        if (asset.Hash == hash)
            return Ok(asset, wasDuplicate: true); // identical re-upload, nothing to do

        // Different content for an existing path. Strategy: the canonical (served) path always holds
        // the current bytes so the CDN URL stays stable; historical versions are moved into a
        // ".versions" sub-folder. Each version's bytes are therefore preserved exactly once.
        var nameNoExt = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        var versionsRelDir = CombineRel(asset.Folder, ".versions", nameNoExt);
        var versionsAbsDir = ResolveWithinRoot(versionsRelDir)!;
        Directory.CreateDirectory(versionsAbsDir);

        var versions = await _db.FileVersions.Where(v => v.FileAssetId == asset.Id).ToListAsync(cancellationToken);
        var maxVersion = versions.Count == 0 ? 0 : versions.Max(v => v.VersionNumber);

        // The version row that currently maps to the canonical bytes (synthesize one for legacy assets).
        var currentRow = versions.FirstOrDefault(v => v.IsCurrent);
        if (currentRow is null)
        {
            maxVersion = Math.Max(maxVersion, 1);
            currentRow = new FileVersion
            {
                FileAssetId = asset.Id,
                VersionNumber = maxVersion,
                PhysicalPath = asset.PhysicalPath,
                Hash = asset.Hash,
                Size = asset.Size,
                CreatedBy = asset.CreatedBy,
                CreatedDate = asset.CreatedDate,
                IsCurrent = true
            };
            _db.FileVersions.Add(currentRow);
        }

        // Move the current canonical bytes into the versions folder so they survive the overwrite.
        if (File.Exists(asset.PhysicalPath))
        {
            var archivedPath = Path.Combine(versionsAbsDir, $"{currentRow.VersionNumber}{ext}");
            MoveOverwrite(asset.PhysicalPath, archivedPath);
            currentRow.PhysicalPath = archivedPath;
        }
        currentRow.IsCurrent = false;

        // Promote the uploaded bytes to the canonical path as the new current version.
        var newVersion = maxVersion + 1;
        MoveOverwrite(tempPath, asset.PhysicalPath);

        _db.FileVersions.Add(new FileVersion
        {
            FileAssetId = asset.Id,
            VersionNumber = newVersion,
            PhysicalPath = asset.PhysicalPath,
            Hash = hash,
            Size = size,
            CreatedBy = request.UserName,
            CreatedDate = DateTime.UtcNow,
            IsCurrent = true
        });

        asset.Hash = hash;
        asset.Size = size;
        asset.MimeType = MimeTypes.GetMimeType(fileName);
        asset.UpdatedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        await TryGenerateImageVariantsAsync(asset.PhysicalPath, fileName, cancellationToken);

        return Ok(asset, wasDuplicate: false, wasNewVersion: true);
    }

    public async Task<FileDownload?> OpenReadAsync(int id, CancellationToken cancellationToken = default)
    {
        var f = await _db.Files.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
        if (f is null || !File.Exists(f.PhysicalPath))
            return null;

        var stream = new FileStream(f.PhysicalPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        return new FileDownload
        {
            Stream = stream,
            FileName = f.OriginalFileName,
            MimeType = string.IsNullOrEmpty(f.MimeType) ? MimeTypes.GetMimeType(f.FileName) : f.MimeType
        };
    }

    public async Task RegisterPhysicalFileAsync(string physicalPath, string originalFileName, string userName,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(physicalPath))
            return;

        var fullRoot = Path.GetFullPath(Root);
        var fullPath = Path.GetFullPath(physicalPath);
        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            return;

        var relative = Path.GetRelativePath(fullRoot, fullPath).Replace('\\', '/');
        var folder = relative.Split('/')[0];
        var fileName = Path.GetFileName(fullPath);
        var size = new FileInfo(fullPath).Length;
        var hash = await ComputeHashAsync(fullPath, cancellationToken);

        var existing = await _db.Files.FirstOrDefaultAsync(f => f.RelativePath == relative && !f.IsDeleted, cancellationToken);
        if (existing is null)
        {
            var asset = NewAsset(fileName, originalFileName, size, hash, userName, fullPath, relative, folder);
            _db.Files.Add(asset);
        }
        else if (existing.Hash != hash)
        {
            existing.Hash = hash;
            existing.Size = size;
            existing.UpdatedDate = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await TryGenerateImageVariantsAsync(fullPath, fileName, cancellationToken);
    }

    // ----- helpers -----

    private FileAsset NewAsset(string fileName, string originalName, long size, string hash, string user,
        string physicalPath, string relativePath, string folder) => new()
    {
        FileName = fileName,
        OriginalFileName = originalName,
        Extension = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant(),
        MimeType = MimeTypes.GetMimeType(fileName),
        Size = size,
        PhysicalPath = physicalPath,
        RelativePath = relativePath,
        Folder = folder,
        Hash = hash,
        CreatedBy = user,
        CreatedDate = DateTime.UtcNow
    };

    private async Task TryGenerateImageVariantsAsync(string physicalPath, string fileName, CancellationToken ct)
    {
        if (_validator.IsImage(fileName))
            await _imageProcessor.GenerateVariantsAsync(physicalPath, ct);
    }

    private UploadResultDto Ok(FileAsset asset, bool wasDuplicate, bool wasNewVersion = false) => new()
    {
        Success = true,
        WasDuplicate = wasDuplicate,
        WasNewVersion = wasNewVersion,
        File = new FileDto
        {
            Id = asset.Id,
            FileName = asset.FileName,
            OriginalFileName = asset.OriginalFileName,
            Extension = asset.Extension,
            MimeType = asset.MimeType,
            Size = asset.Size,
            RelativePath = asset.RelativePath,
            Folder = asset.Folder,
            Hash = asset.Hash,
            CdnUrl = BuildCdnUrl(asset.RelativePath),
            CreatedBy = asset.CreatedBy,
            CreatedDate = asset.CreatedDate
        }
    };

    private async Task<(string tempPath, long size, string hash)> WriteTempAsync(Stream content, CancellationToken ct)
    {
        var tempDir = Path.Combine(Root, "temp");
        Directory.CreateDirectory(tempDir);
        var tempPath = Path.Combine(tempDir, Guid.NewGuid().ToString("N") + ".upload");

        using var sha = SHA256.Create();
        long total = 0;
        var buffer = new byte[81920];

        await using (var fs = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            int read;
            while ((read = await content.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
            {
                total += read;
                if (total > _options.MaxUploadSizeBytes)
                {
                    fs.Close();
                    SafeDelete(tempPath);
                    throw new UploadTooLargeException();
                }
                sha.TransformBlock(buffer, 0, read, null, 0);
                await fs.WriteAsync(buffer.AsMemory(0, read), ct);
            }
            sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        }

        return (tempPath, total, Convert.ToHexString(sha.Hash!).ToLowerInvariant());
    }

    private static async Task<string> ComputeHashAsync(string path, CancellationToken ct)
    {
        await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(fs, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private string BuildRelativePath(string folder, string? subPath, string fileName)
    {
        var parts = new List<string> { SanitizeSegment(folder) };
        if (!string.IsNullOrWhiteSpace(subPath))
            parts.AddRange(subPath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(SanitizeSegment));
        parts.Add(fileName);
        return string.Join('/', parts.Where(p => !string.IsNullOrEmpty(p)));
    }

    private static string CombineRel(params string[] segments) =>
        string.Join('/', segments.Where(s => !string.IsNullOrWhiteSpace(s)));

    /// <summary>Resolves a relative path under the root, guarding against traversal. Null if it escapes the root.</summary>
    private string? ResolveWithinRoot(string relativePath)
    {
        var fullRoot = Path.GetFullPath(Root);
        var combined = Path.GetFullPath(Path.Combine(fullRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        return combined.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase) ? combined : null;
    }

    private static string SanitizeFileName(string fileName)
    {
        fileName = Path.GetFileName(fileName.Replace('\\', '/'));
        foreach (var c in Path.GetInvalidFileNameChars())
            fileName = fileName.Replace(c, '_');
        return fileName.Trim();
    }

    private static string SanitizeSegment(string segment)
    {
        segment = segment.Trim().Trim('.');
        foreach (var c in Path.GetInvalidFileNameChars())
            segment = segment.Replace(c, '_');
        return segment;
    }

    private static void MoveOverwrite(string source, string dest)
    {
        if (File.Exists(dest)) File.Delete(dest);
        File.Move(source, dest);
    }

    private static void SafeDelete(string path)
    {
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            try { File.Delete(path); } catch { /* best effort */ }
        }
    }

    private sealed class UploadTooLargeException : Exception { }
}
