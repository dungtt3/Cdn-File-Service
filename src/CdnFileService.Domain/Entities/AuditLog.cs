namespace CdnFileService.Domain.Entities;

/// <summary>Records a user-initiated action against the file system for auditing.</summary>
public class AuditLog
{
    public long Id { get; set; }

    public string UserName { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>Upload, Delete, Rename, Move, Download, etc.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Target file/folder path (relative where possible).</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>Optional extra detail (e.g. rename from/to).</summary>
    public string? Details { get; set; }

    public DateTime Timestamp { get; set; }
}
