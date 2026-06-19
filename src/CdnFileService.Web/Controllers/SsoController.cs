using System.Security.Claims;
using CdnFileService.Application.Authorization;
using CdnFileService.Application.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CdnFileService.Web.Controllers;

/// <summary>
/// Single-sign-on entry point: a trusted company site sends an HMAC-signed token so its
/// already-authenticated user can open the File Manager without logging in again. The resulting
/// cookie session is scoped to that company (claims: company + granted FileManager actions).
/// </summary>
[AllowAnonymous]
[Route("sso")]
public class SsoController : Controller
{
    private readonly ISsoTokenService _sso;
    private readonly IAuditService _audit;

    public SsoController(ISsoTokenService sso, IAuditService audit)
    {
        _sso = sso;
        _audit = audit;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string token, string? returnUrl = null)
    {
        var result = _sso.Validate(token);
        if (!result.IsValid)
            return Unauthorized(result.Error);

        var p = result.Principal!;

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, p.UserName),
            new(Permissions.CompanyClaimType, p.CompanyId.ToString())
        };

        // Grant only the per-file actions present in the token (never AllCompanies via SSO).
        var granted = p.Permissions.Where(x => Permissions.FileActions.Contains(x)).ToList();
        if (granted.Count == 0)
            granted.Add(Permissions.View); // baseline so the manager opens
        foreach (var perm in granted)
            claims.Add(new Claim(Permissions.ClaimType, perm));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));

        await _audit.LogAsync(p.UserName, HttpContext.Connection.RemoteIpAddress?.ToString() ?? "-",
            "SsoLogin", $"company/{p.CompanyId}");

        var target = !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/FileManager";
        return Redirect(target);
    }
}
