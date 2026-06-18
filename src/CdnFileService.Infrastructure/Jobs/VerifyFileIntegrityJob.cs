using System.Security.Cryptography;
using CdnFileService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace CdnFileService.Infrastructure.Jobs;

/// <summary>
/// Verifies that stored files still exist and their SHA-256 matches the recorded hash. Runs daily.
/// Logs discrepancies (missing/corrupt) for operator follow-up.
/// </summary>
[DisallowConcurrentExecution]
public class VerifyFileIntegrityJob : IJob
{
    public static readonly JobKey Key = new("verify-integrity");

    private readonly AppDbContext _db;
    private readonly ILogger<VerifyFileIntegrityJob> _logger;

    public VerifyFileIntegrityJob(AppDbContext db, ILogger<VerifyFileIntegrityJob> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        var files = await _db.Files.AsNoTracking()
            .Where(f => !f.IsDeleted)
            .Select(f => new { f.Id, f.PhysicalPath, f.Hash, f.RelativePath })
            .ToListAsync(ct);

        int missing = 0, corrupt = 0;
        foreach (var f in files)
        {
            if (!File.Exists(f.PhysicalPath))
            {
                missing++;
                _logger.LogWarning("Integrity: file missing on disk. Id={Id} Path={Path}", f.Id, f.RelativePath);
                continue;
            }

            var actual = await ComputeHashAsync(f.PhysicalPath, ct);
            if (!string.Equals(actual, f.Hash, StringComparison.OrdinalIgnoreCase))
            {
                corrupt++;
                _logger.LogWarning("Integrity: hash mismatch. Id={Id} Path={Path}", f.Id, f.RelativePath);
            }
        }

        _logger.LogInformation("VerifyFileIntegrityJob checked {Total} file(s): {Missing} missing, {Corrupt} mismatched.",
            files.Count, missing, corrupt);
    }

    private static async Task<string> ComputeHashAsync(string path, CancellationToken ct)
    {
        await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(fs, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
