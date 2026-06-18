using CdnFileService.Application.Common;
using CdnFileService.Application.Interfaces;
using CdnFileService.Infrastructure.Jobs;
using CdnFileService.Infrastructure.Persistence;
using CdnFileService.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace CdnFileService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.Configure<SeedAdminOptions>(configuration.GetSection(SeedAdminOptions.SectionName));

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        services.AddDbContext<AppDbContext>(opt => opt.UseSqlServer(connectionString));

        services.AddScoped<IFileValidator, FileValidator>();
        services.AddScoped<IImageProcessor, ImageSharpProcessor>();
        services.AddScoped<IFileStorageService, FileStorageService>();
        services.AddScoped<IFileMetadataService, FileMetadataService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IUserService, UserService>();

        services.AddQuartzJobs();

        return services;
    }

    private static void AddQuartzJobs(this IServiceCollection services)
    {
        services.AddQuartz(q =>
        {
            // Generate thumbnails every 5 minutes.
            q.AddJob<GenerateThumbnailJob>(j => j.WithIdentity(GenerateThumbnailJob.Key));
            q.AddTrigger(t => t
                .ForJob(GenerateThumbnailJob.Key)
                .WithIdentity("generate-thumbnail-trigger")
                .WithSimpleSchedule(s => s.WithIntervalInMinutes(5).RepeatForever()));

            // Cleanup temp once per day (02:00).
            q.AddJob<CleanupTempJob>(j => j.WithIdentity(CleanupTempJob.Key));
            q.AddTrigger(t => t
                .ForJob(CleanupTempJob.Key)
                .WithIdentity("cleanup-temp-trigger")
                .WithCronSchedule("0 0 2 * * ?"));

            // Verify file integrity once per day (03:00).
            q.AddJob<VerifyFileIntegrityJob>(j => j.WithIdentity(VerifyFileIntegrityJob.Key));
            q.AddTrigger(t => t
                .ForJob(VerifyFileIntegrityJob.Key)
                .WithIdentity("verify-integrity-trigger")
                .WithCronSchedule("0 0 3 * * ?"));
        });

        services.AddQuartzHostedService(opt => opt.WaitForJobsToComplete = true);
    }
}
