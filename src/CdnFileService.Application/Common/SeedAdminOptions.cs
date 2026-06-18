namespace CdnFileService.Application.Common;

/// <summary>Bound from the "SeedAdmin" section. Used to create the initial admin user.</summary>
public class SeedAdminOptions
{
    public const string SectionName = "SeedAdmin";

    public string UserName { get; set; } = "admin";
    public string Password { get; set; } = "Admin@123";
    public string DisplayName { get; set; } = "Administrator";
}
