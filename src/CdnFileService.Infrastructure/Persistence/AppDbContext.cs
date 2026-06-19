using CdnFileService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CdnFileService.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<FileAsset> Files => Set<FileAsset>();
    public DbSet<FileVersion> FileVersions => Set<FileVersion>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<UserClaim> UserClaims => Set<UserClaim>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Tables stay in the existing [dbo] schema but are name-prefixed with "CDN." so they don't
        // collide with other applications sharing the same database, e.g. [dbo].[CDN.UserClaims].

        b.Entity<FileAsset>(e =>
        {
            e.ToTable("CDN.Files");
            e.HasKey(x => x.Id);
            e.Property(x => x.FileName).HasMaxLength(260).IsRequired();
            e.Property(x => x.OriginalFileName).HasMaxLength(260).IsRequired();
            e.Property(x => x.Extension).HasMaxLength(20);
            e.Property(x => x.MimeType).HasMaxLength(127);
            e.Property(x => x.PhysicalPath).HasMaxLength(1024);
            e.Property(x => x.RelativePath).HasMaxLength(1024);
            e.Property(x => x.Folder).HasMaxLength(100);
            e.Property(x => x.Hash).HasMaxLength(64);
            e.Property(x => x.CreatedBy).HasMaxLength(256);
            e.HasIndex(x => x.Hash);
            e.HasIndex(x => x.RelativePath);
            e.HasIndex(x => x.IsDeleted);
            e.HasIndex(x => x.CompanyId);
        });

        b.Entity<FileVersion>(e =>
        {
            e.ToTable("CDN.FileVersions");
            e.HasKey(x => x.Id);
            e.Property(x => x.PhysicalPath).HasMaxLength(1024);
            e.Property(x => x.Hash).HasMaxLength(64);
            e.Property(x => x.CreatedBy).HasMaxLength(256);
            e.HasOne(x => x.FileAsset).WithMany(f => f.Versions)
                .HasForeignKey(x => x.FileAssetId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.FileAssetId, x.VersionNumber }).IsUnique();
        });

        b.Entity<AuditLog>(e =>
        {
            e.ToTable("CDN.AuditLogs");
            e.HasKey(x => x.Id);
            e.Property(x => x.UserName).HasMaxLength(256);
            e.Property(x => x.IpAddress).HasMaxLength(64);
            e.Property(x => x.Action).HasMaxLength(50);
            e.Property(x => x.FilePath).HasMaxLength(1024);
            e.HasIndex(x => x.Timestamp);
        });

        b.Entity<AppUser>(e =>
        {
            e.ToTable("CDN.AppUsers");
            e.HasKey(x => x.Id);
            e.Property(x => x.UserName).HasMaxLength(256).IsRequired();
            e.Property(x => x.DisplayName).HasMaxLength(256);
            e.Property(x => x.PasswordHash).HasMaxLength(512);
            e.Property(x => x.PasswordSalt).HasMaxLength(512);
            e.HasIndex(x => x.UserName).IsUnique();
            e.HasIndex(x => x.CompanyId);
        });

        b.Entity<UserClaim>(e =>
        {
            e.ToTable("CDN.UserClaims");
            e.HasKey(x => x.Id);
            e.Property(x => x.ClaimType).HasMaxLength(256);
            e.Property(x => x.ClaimValue).HasMaxLength(256);
            e.HasOne(x => x.User).WithMany(u => u.Claims)
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
