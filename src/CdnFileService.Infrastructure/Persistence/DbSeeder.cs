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

        if (!await db.AppUsers.AnyAsync(u => u.UserName == seed.UserName, cancellationToken))
        {
            var (hash, salt) = PasswordHasher.Hash(seed.Password);
            var admin = new AppUser
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
    }
}
