using CdnFileService.Application.Authorization;
using CdnFileService.Application.DTOs;
using CdnFileService.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CdnFileService.Web.Controllers.Api;

[ApiController]
[Route("api/files")]
[Authorize(Policy = Permissions.View)]
public class FilesController : ControllerBase
{
    private readonly IFileMetadataService _metadata;
    private readonly IFileStorageService _storage;
    private readonly IAuditService _audit;

    public FilesController(IFileMetadataService metadata, IFileStorageService storage, IAuditService audit)
    {
        _metadata = metadata;
        _storage = storage;
        _audit = audit;
    }

    /// <summary>GET /api/files — list/search file metadata (paged), scoped to the caller's tenant.</summary>
    [HttpGet]
    public async Task<ActionResult<object>> List([FromQuery] FileListQuery query, CancellationToken ct)
    {
        ApplyTenantScope(query);
        var items = await _metadata.ListAsync(query, ct);
        var total = await _metadata.CountAsync(query, ct);
        return Ok(new { total, page = query.Page, pageSize = query.PageSize, items });
    }

    /// <summary>GET /api/files/{id} — metadata for a single file (tenant-checked).</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<FileDto>> Get(int id, CancellationToken ct)
    {
        var file = await _metadata.GetAsync(id, ct);
        if (file is null || !CanAccess(file.CompanyId)) return NotFound();
        return Ok(file);
    }

    /// <summary>POST /api/files/upload — upload a single file (multipart/form-data).</summary>
    [HttpPost("upload")]
    [Authorize(Policy = Permissions.Upload)]
    [RequestSizeLimit(1L * 1024 * 1024 * 1024)]
    public async Task<ActionResult<UploadResultDto>> Upload(
        [FromForm] IFormFile file,
        [FromForm] string folder,
        [FromForm] string? subPath,
        [FromForm] int? companyId,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(UploadResultDto.Failed("No file was provided."));
        if (string.IsNullOrWhiteSpace(folder))
            return BadRequest(UploadResultDto.Failed("A target folder is required."));

        // Tenant: a company user always uploads into their own company; the form value is ignored.
        // A super-admin may target the shared zone (null) or any company via the form value.
        int? targetCompany = IsSuperAdmin ? companyId : CallerCompanyId;

        await using var stream = file.OpenReadStream();
        var result = await _storage.UploadAsync(new UploadRequest
        {
            Content = stream,
            OriginalFileName = file.FileName,
            Folder = folder,
            SubPath = subPath,
            CompanyId = targetCompany,
            UserName = User.Identity?.Name ?? "unknown",
            ContentType = file.ContentType
        }, ct);

        if (!result.Success)
            return BadRequest(result);

        await _audit.LogAsync(User.Identity?.Name ?? "unknown", ClientIp(), "Upload",
            result.File!.RelativePath, result.WasNewVersion ? "new version" : result.WasDuplicate ? "duplicate" : null);

        return Ok(result);
    }

    /// <summary>DELETE /api/files/{id} — soft-delete a file's metadata (tenant-checked).</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permissions.Delete)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var file = await _metadata.GetAsync(id, ct);
        if (file is null || !CanAccess(file.CompanyId)) return NotFound();

        await _metadata.SoftDeleteAsync(id, ct);
        await _audit.LogAsync(User.Identity?.Name ?? "unknown", ClientIp(), "Delete", file.RelativePath);
        return NoContent();
    }

    /// <summary>GET /api/files/download/{id} — stream the file's current bytes (tenant-checked).</summary>
    [HttpGet("download/{id:int}")]
    [Authorize(Policy = Permissions.Download)]
    public async Task<IActionResult> Download(int id, CancellationToken ct)
    {
        var file = await _metadata.GetAsync(id, ct);
        if (file is null || !CanAccess(file.CompanyId)) return NotFound();

        var download = await _storage.OpenReadAsync(id, ct);
        if (download is null) return NotFound();

        await _audit.LogAsync(User.Identity?.Name ?? "unknown", ClientIp(), "Download", download.FileName);
        return File(download.Stream, download.MimeType, download.FileName);
    }

    // ----- tenant helpers (always derived from claims, never trusted from the client) -----

    private bool IsSuperAdmin => User.HasClaim(Permissions.ClaimType, Permissions.AllCompanies);

    private int? CallerCompanyId =>
        int.TryParse(User.FindFirst(Permissions.CompanyClaimType)?.Value, out var id) ? id : null;

    private bool CanAccess(int? fileCompanyId) =>
        IsSuperAdmin || fileCompanyId == CallerCompanyId;

    private void ApplyTenantScope(FileListQuery query)
    {
        if (IsSuperAdmin)
        {
            // Admin sees everything, or one company if explicitly requested via ?companyId=.
            query.AllCompanies = query.CompanyId is null;
        }
        else
        {
            query.AllCompanies = false;
            query.CompanyId = CallerCompanyId; // forced to the caller's tenant (null = shared)
        }
    }

    private string ClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "-";
}
