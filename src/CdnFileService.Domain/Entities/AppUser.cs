namespace CdnFileService.Domain.Entities;

/// <summary>
/// Minimal user store backing cookie authentication. Passwords are stored as a PBKDF2 hash
/// with a per-user salt (never in plain text).
/// </summary>
public class AppUser
{
    public int Id { get; set; }

    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; }

    public ICollection<UserClaim> Claims { get; set; } = new List<UserClaim>();
}
