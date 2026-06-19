namespace CdnFileService.Application.Common;

/// <summary>
/// Bound from the "Sso" section. Shared secret used to verify single-sign-on tokens issued by
/// trusted company sites so their already-authenticated users can open the File Manager without
/// logging in again.
/// </summary>
public class SsoOptions
{
    public const string SectionName = "Sso";

    /// <summary>HMAC-SHA256 secret shared with the consuming company sites. Must be set in production.</summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>Maximum accepted token age (seconds) to limit replay. Default 300s (5 min).</summary>
    public int MaxAgeSeconds { get; set; } = 300;

    public bool Enabled => !string.IsNullOrWhiteSpace(Secret);
}
