using CdnFileService.Application.DTOs;

namespace CdnFileService.Application.Interfaces;

/// <summary>Read/query and soft-delete operations over file metadata.</summary>
public interface IFileMetadataService
{
    Task<IReadOnlyList<FileDto>> ListAsync(FileListQuery query, CancellationToken cancellationToken = default);
    Task<int> CountAsync(FileListQuery query, CancellationToken cancellationToken = default);
    Task<FileDto?> GetAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> SoftDeleteAsync(int id, CancellationToken cancellationToken = default);
}
