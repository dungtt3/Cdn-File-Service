using System.Diagnostics;
using CdnFileService.Application.Authorization;
using CdnFileService.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CdnFileService.Web.Models;

namespace CdnFileService.Web.Controllers;

public class HomeController : Controller
{
    private readonly IFileMetadataService _metadata;

    public HomeController(IFileMetadataService metadata) => _metadata = metadata;

    // Dashboard is super-admin only; company (SSO) users have no access and are denied.
    [Authorize(Policy = Permissions.AllCompanies)]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var recent = await _metadata.ListAsync(new Application.DTOs.FileListQuery { PageSize = 10 }, ct);
        var total = await _metadata.CountAsync(new Application.DTOs.FileListQuery(), ct);
        ViewBag.TotalFiles = total;
        return View(recent);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
