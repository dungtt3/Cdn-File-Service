namespace CdnFileService.Infrastructure.Services;

/// <summary>Maps the supported extensions to canonical MIME types (self-contained, no ASP.NET dependency).</summary>
public static class MimeTypes
{
    private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["js"] = "text/javascript",
        ["css"] = "text/css",
        ["png"] = "image/png",
        ["jpg"] = "image/jpeg",
        ["jpeg"] = "image/jpeg",
        ["gif"] = "image/gif",
        ["svg"] = "image/svg+xml",
        ["webp"] = "image/webp",
        ["pdf"] = "application/pdf",
        ["docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ["xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ["zip"] = "application/zip",
    };

    public static string GetMimeType(string fileName)
    {
        var ext = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
        return Map.TryGetValue(ext, out var mime) ? mime : "application/octet-stream";
    }
}
