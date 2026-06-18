using CdnFileService.Application.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace CdnFileService.Infrastructure.Jobs;

/// <summary>Deletes stale files from the temp folder. Runs daily.</summary>
[DisallowConcurrentExecution]
public class CleanupTempJob : IJob
{
    public static readonly JobKey Key = new("cleanup-temp");

    private static readonly TimeSpan MaxAge = TimeSpan.FromHours(24);
    private readonly StorageOptions _options;
    private readonly ILogger<CleanupTempJob> _logger;

    public CleanupTempJob(IOptions<StorageOptions> options, ILogger<CleanupTempJob> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task Execute(IJobExecutionContext context)
    {
        var tempDir = Path.Combine(_options.RootPath, "temp");
        if (!Directory.Exists(tempDir))
            return Task.CompletedTask;

        var cutoff = DateTime.UtcNow - MaxAge;
        var deleted = 0;
        foreach (var file in Directory.EnumerateFiles(tempDir, "*", SearchOption.AllDirectories))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(file) < cutoff)
                {
                    File.Delete(file);
                    deleted++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete temp file {File}", file);
            }
        }

        if (deleted > 0)
            _logger.LogInformation("CleanupTempJob removed {Count} stale temp file(s).", deleted);
        return Task.CompletedTask;
    }
}
