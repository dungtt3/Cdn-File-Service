using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CdnFileService.Application.Common;
using CdnFileService.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace CdnFileService.Infrastructure.Services;

/// <summary>
/// HMAC-SHA256 SSO tokens. Format: <c>base64url(payloadJson).base64url(signature)</c> where the
/// signature is HMAC-SHA256(secret, payloadBase64url). The payload carries the company id, user
/// name, granted permissions and expiry. A company site reproduces this with only HMACSHA256.
/// </summary>
public class SsoTokenService : ISsoTokenService
{
    private readonly SsoOptions _options;

    public SsoTokenService(IOptions<SsoOptions> options) => _options = options.Value;

    private sealed class Payload
    {
        public int c { get; set; }            // companyId (= MA_CONG_TY)
        public string u { get; set; } = "";   // user name
        public string p { get; set; } = "";   // permissions, comma-separated
        public long e { get; set; }            // expiry, unix seconds
        public string n { get; set; } = "";   // nonce
    }

    public string Generate(int companyId, string userName, IEnumerable<string> permissions, TimeSpan? lifetime = null)
    {
        var payload = new Payload
        {
            c = companyId,
            u = userName,
            p = string.Join(",", permissions),
            e = DateTimeOffset.UtcNow.Add(lifetime ?? TimeSpan.FromMinutes(5)).ToUnixTimeSeconds(),
            n = Guid.NewGuid().ToString("N")
        };
        var payloadB64 = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload));
        var sigB64 = Base64UrlEncode(Sign(payloadB64));
        return payloadB64 + "." + sigB64;
    }

    public SsoValidationResult Validate(string token)
    {
        if (!_options.Enabled)
            return SsoValidationResult.Fail("SSO is not configured.");
        if (string.IsNullOrWhiteSpace(token))
            return SsoValidationResult.Fail("Missing token.");

        var parts = token.Split('.');
        if (parts.Length != 2)
            return SsoValidationResult.Fail("Malformed token.");

        var payloadB64 = parts[0];
        byte[] expectedSig = Sign(payloadB64);
        byte[] actualSig;
        try { actualSig = Base64UrlDecode(parts[1]); }
        catch { return SsoValidationResult.Fail("Malformed signature."); }

        if (!CryptographicOperations.FixedTimeEquals(expectedSig, actualSig))
            return SsoValidationResult.Fail("Invalid signature.");

        Payload? payload;
        try { payload = JsonSerializer.Deserialize<Payload>(Base64UrlDecode(payloadB64)); }
        catch { return SsoValidationResult.Fail("Malformed payload."); }
        if (payload is null)
            return SsoValidationResult.Fail("Malformed payload.");

        // Allow for clock drift between the issuing company site and this server so valid tokens
        // are not rejected as "expired" / "too long" merely because the two clocks disagree.
        const long clockSkewSeconds = 120;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (payload.e < now - clockSkewSeconds)
            return SsoValidationResult.Fail("Token expired.");
        if (payload.e - now > _options.MaxAgeSeconds + clockSkewSeconds)
            return SsoValidationResult.Fail("Token lifetime exceeds the allowed maximum.");
        if (payload.c <= 0 || string.IsNullOrWhiteSpace(payload.u))
            return SsoValidationResult.Fail("Token missing company or user.");

        var perms = (payload.p ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return SsoValidationResult.Ok(new SsoPrincipal(payload.c, payload.u, perms));
    }

    private byte[] Sign(string payloadB64)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.Secret));
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadB64));
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4) { case 2: s += "=="; break; case 3: s += "="; break; }
        return Convert.FromBase64String(s);
    }
}
