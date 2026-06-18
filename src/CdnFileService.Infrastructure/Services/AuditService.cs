using CdnFileService.Application.Interfaces;
using CdnFileService.Domain.Entities;
using CdnFileService.Infrastructure.Persistence;

namespace CdnFileService.Infrastructure.Services;

public class AuditService : IAuditService
{
    private readonly AppDbContext _db;

    public AuditService(AppDbContext db) => _db = db;

    public async Task LogAsync(string userName, string ipAddress, string action, string filePath,
        string? details = null, CancellationToken cancellationToken = default)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            UserName = userName,
            IpAddress = ipAddress,
            Action = action,
            FilePath = filePath,
            Details = details,
            Timestamp = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);
    }
}
