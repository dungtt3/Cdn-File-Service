namespace CdnFileService.Domain.Entities;

/// <summary>
/// Metadata record for a stored asset. Maps to the spec "Files" table.
/// The physical bytes may be shared by several records (see <see cref="Hash"/> de-duplication).
/// </summary>
public class FileAsset
{
    public int Id { get; set; }

    /// <summary>Stored (possibly de-duplicated/normalized) file name on disk.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Original file name as uploaded by the user.</summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>File extension without the leading dot, lower-cased (e.g. "png").</summary>
    public string Extension { get; set; } = string.Empty;

    public string MimeType { get; set; } = string.Empty;

    /// <summary>Size in bytes.</summary>
    public long Size { get; set; }

    /// <summary>Absolute path on the storage volume.</summary>
    public string PhysicalPath { get; set; } = string.Empty;

    /// <summary>Path relative to the storage root, using forward slashes (e.g. "images/logo/logo.png").</summary>
    public string RelativePath { get; set; } = string.Empty;

    /// <summary>Top-level logical folder (js, css, images, documents, fonts, media, temp).</summary>
    public string Folder { get; set; } = string.Empty;

    /// <summary>SHA-256 hash of the file content (hex). Used for duplicate detection.</summary>
    public string Hash { get; set; } = string.Empty;

    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public DateTime? UpdatedDate { get; set; }
    public bool IsDeleted { get; set; }

    public ICollection<FileVersion> Versions { get; set; } = new List<FileVersion>();
}
