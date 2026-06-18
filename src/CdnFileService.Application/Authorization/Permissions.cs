namespace CdnFileService.Application.Authorization;

/// <summary>
/// Claim values for the FileManager module. Each is also used as the authorization
/// policy name. Claim type is always <see cref="ClaimType"/>.
/// </summary>
public static class Permissions
{
    public const string ClaimType = "permission";

    public const string View = "FileManager.View";
    public const string Upload = "FileManager.Upload";
    public const string Edit = "FileManager.Edit";
    public const string Delete = "FileManager.Delete";
    public const string Download = "FileManager.Download";

    public static readonly string[] All = { View, Upload, Edit, Delete, Download };
}
