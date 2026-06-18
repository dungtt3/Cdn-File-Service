using Serilog;
using Serilog.Events;
using Serilog.Sinks.MSSqlServer;

namespace CdnFileService.Infrastructure.Logging;

/// <summary>Centralized Serilog setup writing to a rolling file and a SQL Server table.</summary>
public static class SerilogConfig
{
    public static void Configure(LoggerConfiguration cfg, string? connectionString, string logFilePath)
    {
        cfg.MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File(
                path: logFilePath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 31,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}");

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            cfg.WriteTo.MSSqlServer(
                connectionString: connectionString,
                sinkOptions: new MSSqlServerSinkOptions
                {
                    TableName = "Logs",
                    AutoCreateSqlTable = true
                },
                restrictedToMinimumLevel: LogEventLevel.Warning);
        }
    }
}
