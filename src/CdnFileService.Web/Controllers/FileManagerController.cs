using CdnFileService.Application.Authorization;
using CdnFileService.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CdnFileService.Web.Controllers;

[Authorize(Policy = Permissions.View)]
public class FileManagerController : Controller
{
    private readonly StorageOptions _options;

    public FileManagerController(IOptions<StorageOptions> options) => _options = options.Value;

    public IActionResult Index(int? picker)
    {
        ViewBag.CdnRequestPath = _options.CdnRequestPath;
        ViewBag.IsSuperAdmin = User.HasClaim(Permissions.ClaimType, Permissions.AllCompanies);
        ViewBag.CompanyId = User.FindFirst(Permissions.CompanyClaimType)?.Value;
        // Picker mode: embedded as an iframe by a company site; selecting a file posts its URL
        // back to the parent window instead of acting as a standalone manager.
        ViewBag.Picker = picker == 1;
        return View();
    }
}
