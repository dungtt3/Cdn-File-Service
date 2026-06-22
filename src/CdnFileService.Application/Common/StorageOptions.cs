namespace CdnFileService.Application.Common;

/// <summary>Bound from the "Storage" section of appsettings.json.</summary>
public class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Root directory for all assets. Relative paths are resolved against the content root.</summary>
    public string RootPath { get; set; } = "Storage";

    /// <summary>Public base URL of the CDN (used to generate absolute asset URLs).</summary>
    public string CdnBaseUrl { get; set; } = "https://cdn.company.vn";

    /// <summary>Request path prefix under which assets are served by this app (e.g. "/cdn").</summary>
    public string CdnRequestPath { get; set; } = "/cdn";

    /// <summary>Top-level folders created automatically on startup (the shared zone).</summary>
    public string[] RootFolders { get; set; } =
        { "js", "css", "images", "documents", "fonts", "media", "temp" };

    /// <summary>Folder under the root that holds per-company (tenant) subtrees, e.g. companies/{companyId}/...</summary>
    public string CompaniesFolder { get; set; } = "companies";

    /// <summary>Allowed file extensions (without dot, lower-case).</summary>
    public string[] AllowedExtensions { get; set; } =
        { "js", "css", "png", "jpg", "jpeg", "gif", "svg", "webp", "ico",
          "woff", "woff2", "ttf", "eot", "otf", "pdf", "docx", "xlsx", "zip" };

    /// <summary>Explicitly blocked extensions (takes precedence over the allow-list).</summary>
    public string[] BlockedExtensions { get; set; } =
        { "exe", "bat", "cmd", "ps1", "dll", "msi" };

    /// <summary>Maximum upload size in bytes (default 1 GB).</summary>
    public long MaxUploadSizeBytes { get; set; } = 1L * 1024 * 1024 * 1024;

    /// <summary>Cache-Control max-age (seconds) for served static assets (default 1 year).</summary>
    public int CacheMaxAgeSeconds { get; set; } = 31536000;

    /// <summary>
    ///     Value sent in the <c>Access-Control-Allow-Origin</c> header for served static assets.
    ///     Required so fonts (woff/woff2/ttf/...) referenced cross-origin from other sites'
    ///     stylesheets are not blocked by the browser CORS policy. Reads are public, so "*" is the
    ///     sensible default; set a specific origin to lock it down.
    /// </summary>
    public string CorsAllowOrigin { get; set; } = "*";

    /// <summary>Thumbnail max dimension in pixels.</summary>
    public int ThumbnailSize { get; set; } = 200;

    /// <summary>Image extensions eligible for WebP / thumbnail generation.</summary>
    public string[] ImageExtensions { get; set; } = { "png", "jpg", "jpeg", "gif", "webp" };
}
