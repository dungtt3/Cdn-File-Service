using CdnFileService.Application.DTOs;

namespace CdnFileService.Application.Interfaces;

/// <summary>
/// Orchestrates physical storage + metadata: validation, SHA-256 hashing, duplicate detection,
/// versioning, image processing and CDN URL generation.
/// </summary>
public interface IFileStorageService
{
    Task<UploadResultDto> UploadAsync(UploadRequest request, CancellationToken cancellationToken = default);

    /// <summary>Opens the current bytes of an asset for download, or null if missing/deleted.</summary>
    Task<FileDownload?> OpenReadAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Builds the public CDN URL for a relative path (e.g. "images/logo/logo.png").</summary>
    string BuildCdnUrl(string relativePath);

    /// <summary>
    /// Records metadata for a file that was written directly to the volume by an external
    /// component (e.g. the elFinder connector). Idempotent on (relativePath, hash).
    /// </summary>
    Task RegisterPhysicalFileAsync(string physicalPath, string originalFileName, string userName,
        CancellationToken cancellationToken = default);
}
