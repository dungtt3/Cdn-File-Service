namespace CdnFileService.Domain.Entities;

/// <summary>
/// A historical version of a <see cref="FileAsset"/>. Uploading new content for an existing
/// logical file does not overwrite the previous bytes; a new version row is created so the
/// asset can be rolled back.
/// </summary>
public class FileVersion
{
    public int Id { get; set; }

    public int FileAssetId { get; set; }
    public FileAsset? FileAsset { get; set; }

    /// <summary>1-based, monotonically increasing per asset.</summary>
    public int VersionNumber { get; set; }

    /// <summary>Absolute path of this version's bytes on disk.</summary>
    public string PhysicalPath { get; set; } = string.Empty;

    public string Hash { get; set; } = string.Empty;
    public long Size { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }

    /// <summary>True when this version is the one currently served by the asset.</summary>
    public bool IsCurrent { get; set; }
}
