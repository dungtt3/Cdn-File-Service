using CdnFileService.Application.Authorization;
using CdnFileService.Application.Common;
using CdnFileService.Application.Interfaces;
using CdnFileService.Web.Infrastructure;
using elFinder.Net.AspNetCore.Extensions;
using elFinder.Net.AspNetCore.Helper;
using elFinder.Net.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CdnFileService.Web.Controllers;

/// <summary>elFinder connector endpoints backing the file-manager UI.</summary>
[Authorize(Policy = Permissions.View)]
[Route("el-finder")]
public class ElFinderController : Controller
{
    private readonly IConnector _connector;
    private readonly IDriver _driver;
    private readonly StorageOptions _options;
    private readonly IFileStorageService _storage;
    private readonly IAuditService _audit;
    private readonly IWebHostEnvironment _env;

    public ElFinderController(IConnector connector, IDriver driver, IOptions<StorageOptions> options,
        IFileStorageService storage, IAuditService audit, IWebHostEnvironment env)
    {
        _connector = connector;
        _driver = driver;
        _options = options.Value;
        _storage = storage;
        _audit = audit;
        _env = env;
    }

    [Route("connector")]
    public async Task<IActionResult> Connector()
    {
        await SetupConnectorAsync();
        var cmd = ConnectorHelper.ParseCommand(Request);
        var ccTokenSource = ConnectorHelper.RegisterCcTokenSource(HttpContext);
        var conResult = await _connector.ProcessAsync(cmd, ccTokenSource);
        return conResult.ToActionResult(HttpContext);
    }

    [Route("thumb/{target}")]
    public async Task<IActionResult> Thumb(string target)
    {
        await SetupConnectorAsync();
        var thumb = await _connector.GetThumbAsync(target, HttpContext.RequestAborted);
        return ConnectorHelper.GetThumbResult(thumb);
    }

    private async Task SetupConnectorAsync()
    {
        var root = _options.RootPath;
        var contentRoot = _env.ContentRootPath;

        var volume = new Volume(_driver,
            rootDirectory: root,
            tempDirectory: ElFinderPaths.Quarantine(contentRoot),
            url: _options.CdnRequestPath,
            thumbUrl: ElFinderPaths.ThumbRequestPath,
            tempArchiveDirectory: ElFinderPaths.Archive(contentRoot),
            chunkDirectory: ElFinderPaths.Chunk(contentRoot),
            thumbnailDirectory: ElFinderPaths.Thumb(contentRoot),
            directorySeparatorChar: Path.DirectorySeparatorChar)
        {
            Name = "Storage",
            MaxUploadSize = _options.MaxUploadSizeBytes
        };

        var userName = User.Identity?.Name ?? "unknown";
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "-";

        // Mirror elFinder uploads into the metadata DB (+ image variants), and audit them.
        _driver.OnAfterUpload.Add(async (file, destFile, formFile, isOverwrite, isChunking) =>
        {
            await _storage.RegisterPhysicalFileAsync(destFile.FullName, formFile.FileName, userName);
            await _audit.LogAsync(userName, ip, "Upload", destFile.FullName);
        });

        _connector.AddVolume(volume);
        await _driver.SetupVolumeAsync(volume);
    }
}
