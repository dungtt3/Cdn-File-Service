namespace CdnFileService.Application.Interfaces;

public record ImageVariants(string? WebpPath, string? ThumbnailPath);

/// <summary>Generates derived image assets (WebP + thumbnail) next to the source image.</summary>
public interface IImageProcessor
{
    /// <summary>
    /// Produces <c>name.webp</c> and <c>name_thumb.webp</c> beside <paramref name="physicalPath"/>.
    /// Returns the generated paths (null entries if not applicable).
    /// </summary>
    Task<ImageVariants> GenerateVariantsAsync(string physicalPath, CancellationToken cancellationToken = default);
}
