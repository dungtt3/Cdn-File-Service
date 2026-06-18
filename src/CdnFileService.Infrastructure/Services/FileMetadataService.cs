using CdnFileService.Application.DTOs;
using CdnFileService.Application.Interfaces;
using CdnFileService.Domain.Entities;
using CdnFileService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CdnFileService.Infrastructure.Services;

public class FileMetadataService : IFileMetadataService
{
    private readonly AppDbContext _db;
    private readonly IFileStorageService _storage;

    public FileMetadataService(AppDbContext db, IFileStorageService storage)
    {
        _db = db;
        _storage = storage;
    }

    public async Task<IReadOnlyList<FileDto>> ListAsync(FileListQuery query, CancellationToken cancellationToken = default)
    {
        var q = BuildQuery(query);
        var page = Math.Max(1, query.Page);
        var size = Math.Clamp(query.PageSize, 1, 500);

        var items = await q
            .OrderByDescending(f => f.CreatedDate)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        return items.Select(ToDto).ToList();
    }

    public Task<int> CountAsync(FileListQuery query, CancellationToken cancellationToken = default)
        => BuildQuery(query).CountAsync(cancellationToken);

    public async Task<FileDto?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        var f = await _db.Files.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
        return f is null ? null : ToDto(f);
    }

    public async Task<bool> SoftDeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var f = await _db.Files.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
        if (f is null) return false;
        f.IsDeleted = true;
        f.UpdatedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private IQueryable<FileAsset> BuildQuery(FileListQuery query)
    {
        var q = _db.Files.AsNoTracking().Where(f => !f.IsDeleted);
        if (!string.IsNullOrWhiteSpace(query.Folder))
            q = q.Where(f => f.Folder == query.Folder);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(f => f.OriginalFileName.Contains(s) || f.RelativePath.Contains(s));
        }
        return q;
    }

    private FileDto ToDto(FileAsset f) => new()
    {
        Id = f.Id,
        FileName = f.FileName,
        OriginalFileName = f.OriginalFileName,
        Extension = f.Extension,
        MimeType = f.MimeType,
        Size = f.Size,
        RelativePath = f.RelativePath,
        Folder = f.Folder,
        Hash = f.Hash,
        CdnUrl = _storage.BuildCdnUrl(f.RelativePath),
        CreatedBy = f.CreatedBy,
        CreatedDate = f.CreatedDate
    };
}
