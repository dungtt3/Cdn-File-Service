namespace CdnFileService.Application.DTOs;

/// <summary>Outcome of an upload through the storage service.</summary>
public class UploadResultDto
{
    public bool Success { get; set; }
    public string? Error { get; set; }

    /// <summary>True when an identical file (same hash) already existed and bytes were reused.</summary>
    public bool WasDuplicate { get; set; }

    /// <summary>True when this upload created a new version of an existing logical file.</summary>
    public bool WasNewVersion { get; set; }

    public FileDto? File { get; set; }

    public static UploadResultDto Failed(string error) => new() { Success = false, Error = error };
}
