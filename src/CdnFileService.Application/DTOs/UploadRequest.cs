namespace CdnFileService.Application.DTOs;

/// <summary>Input for an upload through <see cref="Interfaces.IFileStorageService"/>.</summary>
public class UploadRequest
{
    public required Stream Content { get; init; }
    public required string OriginalFileName { get; init; }

    /// <summary>Top-level folder (js, css, images, ...).</summary>
    public required string Folder { get; init; }

    /// <summary>Optional sub-path under the folder, e.g. "logo" → images/logo.</summary>
    public string? SubPath { get; init; }

    /// <summary>Owning tenant (= MA_CONG_TY). Null = shared zone (super-admin only).</summary>
    public int? CompanyId { get; init; }

    public required string UserName { get; init; }
    public string? ContentType { get; init; }
}

/// <summary>A readable file plus the metadata needed to stream it back to a client.</summary>
public class FileDownload
{
    public required Stream Stream { get; init; }
    public required string FileName { get; init; }
    public required string MimeType { get; init; }
}
