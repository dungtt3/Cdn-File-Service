namespace CdnFileService.Application.Authorization;

/// <summary>
/// Claim values for the FileManager module. Each is also used as the authorization
/// policy name. Claim type is always <see cref="ClaimType"/>.
/// </summary>
public static class Permissions
{
    public const string ClaimType = "permission";

    /// <summary>Claim type carrying the user's tenant id (= MA_CONG_TY). Absent/empty = all companies.</summary>
    public const string CompanyClaimType = "company";

    public const string View = "FileManager.View";
    public const string Upload = "FileManager.Upload";
    public const string Edit = "FileManager.Edit";
    public const string Delete = "FileManager.Delete";
    public const string Download = "FileManager.Download";

    /// <summary>Super-admin: may manage the shared zone and every company's files.</summary>
    public const string AllCompanies = "FileManager.AllCompanies";

    /// <summary>The five per-action permissions granted to normal (company-scoped) users.</summary>
    public static readonly string[] FileActions = { View, Upload, Edit, Delete, Download };

    /// <summary>All assignable permissions (used to seed the super-admin).</summary>
    public static readonly string[] All = { View, Upload, Edit, Delete, Download, AllCompanies };
}
