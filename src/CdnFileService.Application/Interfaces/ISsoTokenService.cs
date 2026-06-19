namespace CdnFileService.Application.Interfaces;

/// <summary>Validated payload of an SSO token from a trusted company site.</summary>
public record SsoPrincipal(int CompanyId, string UserName, IReadOnlyList<string> Permissions);

public record SsoValidationResult(bool IsValid, string? Error, SsoPrincipal? Principal)
{
    public static SsoValidationResult Fail(string error) => new(false, error, null);
    public static SsoValidationResult Ok(SsoPrincipal p) => new(true, null, p);
}

/// <summary>
/// Verifies the HMAC-signed SSO token issued by a company site and (for testing/tooling) can
/// generate one. The token carries the company id (= MA_CONG_TY), user name, granted permissions
/// and an expiry.
/// </summary>
public interface ISsoTokenService
{
    SsoValidationResult Validate(string token);

    /// <summary>Generates a token (used by tests/tooling; company sites generate their own).</summary>
    string Generate(int companyId, string userName, IEnumerable<string> permissions, TimeSpan? lifetime = null);
}
