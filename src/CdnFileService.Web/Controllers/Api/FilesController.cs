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

    /// <summary>GET /api/files — list/search file metadata (paged).</summary>
    [HttpGet]
    public async Task<ActionResult<object>> List([FromQuery] FileListQuery query, CancellationToken ct)
    {
        var items = await _metadata.ListAsync(query, ct);
        var total = await _metadata.CountAsync(query, ct);
        return Ok(new { total, page = query.Page, pageSize = query.PageSize, items });
    }

    /// <summary>GET /api/files/{id} — metadata for a single file.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<FileDto>> Get(int id, CancellationToken ct)
    {
        var file = await _metadata.GetAsync(id, ct);
        return file is null ? NotFound() : Ok(file);
    }

    /// <summary>POST /api/files/upload — upload a single file (multipart/form-data).</summary>
    [HttpPost("upload")]
    [Authorize(Policy = Permissions.Upload)]
    [RequestSizeLimit(1L * 1024 * 1024 * 1024)]
    public async Task<ActionResult<UploadResultDto>> Upload(
        [FromForm] IFormFile file,
        [FromForm] string folder,
        [FromForm] string? subPath,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(UploadResultDto.Failed("No file was provided."));
        if (string.IsNullOrWhiteSpace(folder))
            return BadRequest(UploadResultDto.Failed("A target folder is required."));

        await using var stream = file.OpenReadStream();
        var result = await _storage.UploadAsync(new UploadRequest
        {
            Content = stream,
            OriginalFileName = file.FileName,
            Folder = folder,
            SubPath = subPath,
            UserName = User.Identity?.Name ?? "unknown",
            ContentType = file.ContentType
        }, ct);

        if (!result.Success)
            return BadRequest(result);

        await _audit.LogAsync(User.Identity?.Name ?? "unknown", ClientIp(), "Upload",
            result.File!.RelativePath, result.WasNewVersion ? "new version" : result.WasDuplicate ? "duplicate" : null);

        return Ok(result);
    }

    /// <summary>DELETE /api/files/{id} — soft-delete a file's metadata.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permissions.Delete)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var file = await _metadata.GetAsync(id, ct);
        if (file is null) return NotFound();

        await _metadata.SoftDeleteAsync(id, ct);
        await _audit.LogAsync(User.Identity?.Name ?? "unknown", ClientIp(), "Delete", file.RelativePath);
        return NoContent();
    }

    /// <summary>GET /api/files/download/{id} — stream the file's current bytes.</summary>
    [HttpGet("download/{id:int}")]
    [Authorize(Policy = Permissions.Download)]
    public async Task<IActionResult> Download(int id, CancellationToken ct)
    {
        var download = await _storage.OpenReadAsync(id, ct);
        if (download is null) return NotFound();

        await _audit.LogAsync(User.Identity?.Name ?? "unknown", ClientIp(), "Download", download.FileName);
        return File(download.Stream, download.MimeType, download.FileName);
    }

    private string ClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "-";
}
