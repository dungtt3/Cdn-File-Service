namespace CdnFileService.Web.Infrastructure;

/// <summary>
/// elFinder working directories. These must live OUTSIDE the storage root — the connector
/// rejects temp/chunk/thumbnail folders that are nested within the volume root.
/// </summary>
public static class ElFinderPaths
{
    public const string ThumbRequestPath = "/el-finder-tmb";

    public static string WorkRoot(string contentRoot) => Path.Combine(contentRoot, "App_Data", "elfinder");
    public static string Quarantine(string contentRoot) => Path.Combine(WorkRoot(contentRoot), "quarantine");
    public static string Archive(string contentRoot) => Path.Combine(WorkRoot(contentRoot), "archive");
    public static string Chunk(string contentRoot) => Path.Combine(WorkRoot(contentRoot), "chunk");
    public static string Thumb(string contentRoot) => Path.Combine(WorkRoot(contentRoot), "thumb");
}
