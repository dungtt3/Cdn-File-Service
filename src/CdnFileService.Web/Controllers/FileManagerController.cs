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

    public IActionResult Index()
    {
        ViewBag.CdnRequestPath = _options.CdnRequestPath;
        return View();
    }
}
