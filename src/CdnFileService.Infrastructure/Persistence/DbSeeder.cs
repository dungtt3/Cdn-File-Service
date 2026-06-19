using CdnFileService.Application.Authorization;
using CdnFileService.Application.Common;
using CdnFileService.Domain.Entities;
using CdnFileService.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CdnFileService.Infrastructure.Persistence;

/// <summary>Applies pending migrations and seeds the initial admin user with all permissions.</summary>
public static class DbSeeder
{
    public static async Task MigrateAndSeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var logger = sp.GetRequiredService<ILogger<AppDbContext>>();
        var seed = sp.GetRequiredService<IOptions<SeedAdminOptions>>().Value;

        await db.Database.MigrateAsync(cancellationToken);

        var admin = await db.AppUsers
            .Include(u => u.Claims)
            .FirstOrDefaultAsync(u => u.UserName == seed.UserName, cancellationToken);

        if (admin is null)
        {
            var (hash, salt) = PasswordHasher.Hash(seed.Password);
            admin = new AppUser
            {
                UserName = seed.UserName,
                DisplayName = seed.DisplayName,
                PasswordHash = hash,
                PasswordSalt = salt,
                IsActive = true,
                CreatedDate = DateTime.UtcNow,
                Claims = Permissions.All
                    .Select(p => new UserClaim { ClaimType = Permissions.ClaimType, ClaimValue = p })
                    .ToList()
            };
            db.AppUsers.Add(admin);
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded admin user '{User}' with all FileManager permissions.", seed.UserName);
        }
        else
        {
            // Upgrade path: ensure an existing admin has every permission (e.g. the new AllCompanies).
            var missing = Permissions.All
                .Where(p => !admin.Claims.Any(c => c.ClaimType == Permissions.ClaimType && c.ClaimValue == p))
                .ToList();
            if (missing.Count > 0)
            {
                foreach (var p in missing)
                    admin.Claims.Add(new UserClaim { ClaimType = Permissions.ClaimType, ClaimValue = p });
                await db.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Backfilled {Count} permission(s) for admin '{User}': {Perms}",
                    missing.Count, seed.UserName, string.Join(", ", missing));
            }
        }
    }
}
