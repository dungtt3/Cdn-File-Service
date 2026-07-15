using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.IdentityModel.Tokens;

namespace CdnFileService.Web.Infrastructure;

/// <summary>
/// Serializes the cookie authentication ticket as an HMAC-SHA256-signed JWT instead of the default
/// Data Protection payload. Any server configured with the same "Jwt:Secret" can validate the auth
/// cookie, so no shared Data Protection key ring (UNC path) is needed behind the load balancer.
/// </summary>
public class JwtTicketDataFormat : ISecureDataFormat<AuthenticationTicket>
{
    private const string Issuer = "CdnFileService";
    private const string IsPersistentClaim = "auth_persistent";
    private static readonly TimeSpan DefaultLifetime = TimeSpan.FromHours(8);
    private static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(120);

    // Claims that only carry JWT bookkeeping; stripped when rebuilding the principal.
    private static readonly HashSet<string> JwtInternalClaims = new()
    {
        JwtRegisteredClaimNames.Iss, JwtRegisteredClaimNames.Aud, JwtRegisteredClaimNames.Exp,
        JwtRegisteredClaimNames.Nbf, JwtRegisteredClaimNames.Iat, IsPersistentClaim
    };

    private readonly JwtSecurityTokenHandler _handler = new() { MapInboundClaims = false };
    private readonly SigningCredentials _signingCredentials;
    private readonly TokenValidationParameters _validationParameters;

    public JwtTicketDataFormat(string secret)
    {
        if (Encoding.UTF8.GetByteCount(secret) < 32)
            throw new InvalidOperationException("Jwt:Secret must be at least 32 characters (256 bits) for HMAC-SHA256.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        _signingCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        _validationParameters = new TokenValidationParameters
        {
            ValidIssuer = Issuer,
            ValidAudience = Issuer,
            IssuerSigningKey = key,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = ClockSkew
        };
    }

    public string Protect(AuthenticationTicket data) => Protect(data, null);

    public string Protect(AuthenticationTicket data, string? purpose)
    {
        var issued = data.Properties.IssuedUtc ?? DateTimeOffset.UtcNow;
        var expires = data.Properties.ExpiresUtc ?? issued + DefaultLifetime;

        var claims = new List<Claim>(data.Principal.Claims);
        if (data.Properties.IsPersistent)
            claims.Add(new Claim(IsPersistentClaim, "1"));

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Issuer,
            claims: claims,
            notBefore: issued.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: _signingCredentials);
        return _handler.WriteToken(token);
    }

    public AuthenticationTicket? Unprotect(string? protectedText) => Unprotect(protectedText, null);

    public AuthenticationTicket? Unprotect(string? protectedText, string? purpose)
    {
        if (string.IsNullOrEmpty(protectedText))
            return null;

        try
        {
            var principal = _handler.ValidateToken(protectedText, _validationParameters, out var validated);
            var jwt = (JwtSecurityToken)validated;

            var claims = principal.Claims.Where(c => !JwtInternalClaims.Contains(c.Type));
            var identity = new ClaimsIdentity(
                claims, CookieAuthenticationDefaults.AuthenticationScheme, ClaimTypes.Name, ClaimTypes.Role);

            var properties = new AuthenticationProperties
            {
                IssuedUtc = new DateTimeOffset(jwt.ValidFrom),
                ExpiresUtc = new DateTimeOffset(jwt.ValidTo),
                IsPersistent = principal.HasClaim(IsPersistentClaim, "1")
            };

            return new AuthenticationTicket(
                new ClaimsPrincipal(identity), properties, CookieAuthenticationDefaults.AuthenticationScheme);
        }
        catch
        {
            // Invalid/expired/foreign token -> treat as unauthenticated (redirect to login).
            return null;
        }
    }
}
