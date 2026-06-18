namespace CdnFileService.Application.Interfaces;

/// <summary>Persists audit entries for user-initiated file actions.</summary>
public interface IAuditService
{
    Task LogAsync(string userName, string ipAddress, string action, string filePath,
        string? details = null, CancellationToken cancellationToken = default);
}
