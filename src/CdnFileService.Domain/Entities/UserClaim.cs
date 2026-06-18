namespace CdnFileService.Domain.Entities;

/// <summary>A single claim granted to an <see cref="AppUser"/> (drives claims-based authorization).</summary>
public class UserClaim
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public AppUser? User { get; set; }

    public string ClaimType { get; set; } = string.Empty;
    public string ClaimValue { get; set; } = string.Empty;
}
