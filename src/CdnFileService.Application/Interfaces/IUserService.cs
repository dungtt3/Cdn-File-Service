using System.Security.Claims;

namespace CdnFileService.Application.Interfaces;

public record AuthResult(bool Succeeded, string? DisplayName, IReadOnlyList<Claim> Claims)
{
    public static readonly AuthResult Fail = new(false, null, Array.Empty<Claim>());
}

/// <summary>Validates credentials and produces the claims for the auth cookie.</summary>
public interface IUserService
{
    Task<AuthResult> ValidateCredentialsAsync(string userName, string password,
        CancellationToken cancellationToken = default);
}
