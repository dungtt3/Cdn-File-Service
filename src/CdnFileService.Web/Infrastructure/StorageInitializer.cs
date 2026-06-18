using CdnFileService.Application.Common;
using Microsoft.Extensions.Options;

namespace CdnFileService.Web.Infrastructure;

/// <summary>Creates the standard storage folder structure on startup.</summary>
public static class StorageInitializer
{
    public static void EnsureFolders(IServiceProvider services)
    {
        var options = services.GetRequiredService<IOptions<StorageOptions>>().Value;
        Directory.CreateDirectory(options.RootPath);
        foreach (var folder in options.RootFolders)
            Directory.CreateDirectory(Path.Combine(options.RootPath, folder));
    }
}
