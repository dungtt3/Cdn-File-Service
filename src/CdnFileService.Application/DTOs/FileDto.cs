namespace CdnFileService.Application.DTOs;

/// <summary>Read model for a stored asset returned by the API/UI.</summary>
public class FileDto
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long Size { get; set; }
    public string RelativePath { get; set; } = string.Empty;
    public string Folder { get; set; } = string.Empty;
    public int? CompanyId { get; set; }
    public string Hash { get; set; } = string.Empty;
    public string CdnUrl { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}
