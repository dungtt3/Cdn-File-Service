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
        var userName = User.Identity?.Name ?? "unknown";
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "-";

        // Mirror elFinder uploads into the metadata DB (+ image variants), and audit them.
        // RegisterPhysicalFileAsync infers the company from the path (companies/{id}/...).
        _driver.OnAfterUpload.Add(async (file, destFile, formFile, isOverwrite, isChunking) =>
        {
            await _storage.RegisterPhysicalFileAsync(destFile.FullName, formFile.FileName, userName);
            await _audit.LogAsync(userName, ip, "Upload", destFile.FullName);
        });

        var isSuperAdmin = User.HasClaim(Permissions.ClaimType, Permissions.AllCompanies);
        if (isSuperAdmin)
        {
            // Super-admin manages the whole volume (shared + every company), read-write.
            var all = BuildVolume(root, "all", _options.CdnRequestPath, "All storage", readOnly: false, hideCompanies: false);
            _connector.AddVolume(all);
            await _driver.SetupVolumeAsync(all);
            return;
        }

        // Company user: show ONLY their own company folder (the shared zone is not mounted).
        if (TryGetCompanyId(out var companyId))
        {
            var companyRoot = Path.Combine(root, _options.CompaniesFolder, companyId.ToString());
            Directory.CreateDirectory(companyRoot);
            var companyUrl = $"{_options.CdnRequestPath.TrimEnd('/')}/{_options.CompaniesFolder}/{companyId}";
            var company = BuildVolume(companyRoot, $"c{companyId}", companyUrl, $"Company {companyId}", readOnly: false, hideCompanies: false);
            _connector.AddVolume(company);
            await _driver.SetupVolumeAsync(company);
            return;
        }

        // No company context (should not happen for SSO sessions): fall back to the shared zone
        // read-only so the file manager still has a volume to open.
        var shared = BuildVolume(root, "shared", _options.CdnRequestPath, "Shared", readOnly: true, hideCompanies: true);
        _connector.AddVolume(shared);
        await _driver.SetupVolumeAsync(shared);
    }

    private bool TryGetCompanyId(out int companyId)
    {
        companyId = 0;
        var claim = User.FindFirst(Permissions.CompanyClaimType)?.Value;
        return !string.IsNullOrEmpty(claim) && int.TryParse(claim, out companyId);
    }

    private Volume BuildVolume(string rootDirectory, string key, string url, string name, bool readOnly, bool hideCompanies)
    {
        var contentRoot = _env.ContentRootPath;
        var work = ElFinderPaths.WorkRoot(contentRoot);

        var volume = new Volume(_driver,
            rootDirectory: rootDirectory,
            tempDirectory: Path.Combine(work, "quarantine", key),
            url: url,
            thumbUrl: $"{ElFinderPaths.ThumbRequestPath}/{key}",
            tempArchiveDirectory: Path.Combine(work, "archive", key),
            chunkDirectory: Path.Combine(work, "chunk", key),
            thumbnailDirectory: Path.Combine(ElFinderPaths.Thumb(contentRoot), key),
            directorySeparatorChar: Path.DirectorySeparatorChar)
        {
            Name = name,
            MaxUploadSize = _options.MaxUploadSizeBytes,
            IsReadOnly = readOnly
        };

        if (hideCompanies)
        {
            // Hide (and deny access to) the per-company subtree from the shared volume so a company
            // user cannot browse other tenants' files.
            var companiesAbs = Path.GetFullPath(Path.Combine(rootDirectory, _options.CompaniesFolder));
            volume.ObjectAttributes = new[]
            {
                new FilteredObjectAttribute
                {
                    ObjectFilter = o => string.Equals(
                        Path.GetFullPath(o.FullName), companiesAbs, StringComparison.OrdinalIgnoreCase),
                    Visible = false,
                    Read = false,
                    Write = false,
                    Access = false
                }
            };
        }

        return volume;
    }
}
