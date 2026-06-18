namespace CdnFileService.Application.DTOs;

/// <summary>Filtering/paging options for listing files.</summary>
public class FileListQuery
{
    public string? Folder { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
