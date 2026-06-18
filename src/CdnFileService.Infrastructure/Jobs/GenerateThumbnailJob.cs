using CdnFileService.Application.Common;
using CdnFileService.Application.Interfaces;
using CdnFileService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace CdnFileService.Infrastructure.Jobs;

/// <summary>
/// Scans recent image assets and ensures a WebP thumbnail exists. Runs every 5 minutes.
/// (Working-core implementation: regenerates variants where the thumbnail file is missing.)
/// </summary>
[DisallowConcurrentExecution]
public class GenerateThumbnailJob : IJob
{
    public static readonly JobKey Key = new("generate-thumbnail");

    private readonly AppDbContext _db;
    private readonly IImageProcessor _imageProcessor;
    private readonly IFileValidator _validator;
    private readonly StorageOptions _options;
    private readonly ILogger<GenerateThumbnailJob> _logger;

    public GenerateThumbnailJob(AppDbContext db, IImageProcessor imageProcessor, IFileValidator validator,
        IOptions<StorageOptions> options, ILogger<GenerateThumbnailJob> logger)
    {
        _db = db;
        _imageProcessor = imageProcessor;
        _validator = validator;
        _options = options.Value;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        var since = DateTime.UtcNow.AddDays(-1);

        var recentImages = await _db.Files
            .AsNoTracking()
            .Where(f => !f.IsDeleted && f.CreatedDate >= since)
            .Select(f => new { f.PhysicalPath, f.FileName })
            .ToListAsync(ct);

        var generated = 0;
        foreach (var img in recentImages)
        {
            if (!_validator.IsImage(img.FileName) || !File.Exists(img.PhysicalPath))
                continue;

            var dir = Path.GetDirectoryName(img.PhysicalPath)!;
            var thumb = Path.Combine(dir, Path.GetFileNameWithoutExtension(img.PhysicalPath) + "_thumb.webp");
            if (File.Exists(thumb))
                continue;

            await _imageProcessor.GenerateVariantsAsync(img.PhysicalPath, ct);
            generated++;
        }

        if (generated > 0)
            _logger.LogInformation("GenerateThumbnailJob produced variants for {Count} image(s).", generated);
    }
}
