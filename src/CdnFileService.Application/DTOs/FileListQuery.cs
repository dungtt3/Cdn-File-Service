namespace CdnFileService.Application.DTOs;

/// <summary>Filtering/paging options for listing files.</summary>
public class FileListQuery
{
    public string? Folder { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;

    /// <summary>Restrict to one tenant (= MA_CONG_TY). Null with <see cref="AllCompanies"/>=false means the shared zone.</summary>
    public int? CompanyId { get; set; }

    /// <summary>Super-admin only: when true, do not filter by company (see everything).</summary>
    public bool AllCompanies { get; set; }
}
