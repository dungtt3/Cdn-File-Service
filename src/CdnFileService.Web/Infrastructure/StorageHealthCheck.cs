using CdnFileService.Application.Common;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace CdnFileService.Web.Infrastructure;

/// <summary>Verifies the storage root exists and is writable.</summary>
public class StorageHealthCheck : IHealthCheck
{
    private readonly StorageOptions _options;

    public StorageHealthCheck(IOptions<StorageOptions> options) => _options = options.Value;

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!Directory.Exists(_options.RootPath))
                return Task.FromResult(HealthCheckResult.Unhealthy($"Storage root '{_options.RootPath}' does not exist."));

            var probe = Path.Combine(_options.RootPath, $".health-{Guid.NewGuid():N}");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);

            return Task.FromResult(HealthCheckResult.Healthy("Storage is writable."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Storage is not writable.", ex));
        }
    }
}
