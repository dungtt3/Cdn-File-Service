using CdnFileService.Application.Common;
using CdnFileService.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace CdnFileService.Infrastructure.Services;

/// <summary>Generates a WebP copy and a WebP thumbnail beside a source image using ImageSharp.</summary>
public class ImageSharpProcessor : IImageProcessor
{
    private readonly StorageOptions _options;
    private readonly ILogger<ImageSharpProcessor> _logger;

    public ImageSharpProcessor(IOptions<StorageOptions> options, ILogger<ImageSharpProcessor> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ImageVariants> GenerateVariantsAsync(string physicalPath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(physicalPath))
            return new ImageVariants(null, null);

        var dir = Path.GetDirectoryName(physicalPath)!;
        var name = Path.GetFileNameWithoutExtension(physicalPath);
        var webpPath = Path.Combine(dir, name + ".webp");
        var thumbPath = Path.Combine(dir, name + "_thumb.webp");

        try
        {
            using var image = await Image.LoadAsync(physicalPath, cancellationToken);

            // Full-size WebP (skip if the source already is .webp to avoid clobbering it).
            if (!string.Equals(Path.GetExtension(physicalPath), ".webp", StringComparison.OrdinalIgnoreCase))
            {
                await image.SaveAsync(webpPath, new WebpEncoder(), cancellationToken);
            }
            else
            {
                webpPath = physicalPath;
            }

            // Thumbnail (max dimension = ThumbnailSize, preserve aspect ratio).
            using var thumb = image.Clone(ctx => ctx.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(_options.ThumbnailSize, _options.ThumbnailSize)
            }));
            await thumb.SaveAsync(thumbPath, new WebpEncoder(), cancellationToken);

            return new ImageVariants(webpPath, thumbPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate image variants for {Path}", physicalPath);
            return new ImageVariants(null, null);
        }
    }
}
