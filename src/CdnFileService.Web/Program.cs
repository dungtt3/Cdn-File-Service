using System.IO.Compression;
using CdnFileService.Application.Authorization;
using CdnFileService.Application.Common;
using CdnFileService.Infrastructure;
using CdnFileService.Infrastructure.Logging;
using CdnFileService.Infrastructure.Persistence;
using CdnFileService.Web.Infrastructure;
using elFinder.Net.AspNetCore.Extensions;
using elFinder.Net.Drivers.FileSystem;
using elFinder.Net.Drivers.FileSystem.Extensions;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

const long maxUploadBytes = 1L * 1024 * 1024 * 1024; // 1 GB

// --- Serilog (file + SQL Server) ---
builder.Host.UseSerilog((ctx, _, cfg) =>
{
    var conn = ctx.Configuration.GetConnectionString("DefaultConnection");
    var logPath = Path.Combine(builder.Environment.ContentRootPath, "logs", "log-.txt");
    SerilogConfig.Configure(cfg, conn, logPath);
});

// --- Application/Infrastructure services ---
builder.Services.AddInfrastructure(builder.Configuration);

// Resolve the storage root to an absolute path under the content root.
builder.Services.PostConfigure<StorageOptions>(o =>
{
    if (!Path.IsPathRooted(o.RootPath))
        o.RootPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, o.RootPath));
});

// --- elFinder file manager ---
builder.Services.AddElFinderAspNetCore();
builder.Services.AddFileSystemDriver(typeof(FileSystemDriver));

// --- Authentication & claims-based authorization ---
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Account/Login";
        o.LogoutPath = "/Account/Logout";
        o.AccessDeniedPath = "/Account/Denied";
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.SlidingExpiration = true;
        // Lax works for the top-level redirect SSO flow (menu link / new tab).
        // For cross-site iframe embedding, change to SameSiteMode.None + Secure (HTTPS only).
        o.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
    });

builder.Services.AddAuthorization(o =>
{
    foreach (var permission in Permissions.All)
        o.AddPolicy(permission, p => p.RequireClaim(Permissions.ClaimType, permission));
});

// --- MVC + API ---
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();

// --- Response compression (gzip + brotli) ---
builder.Services.AddResponseCompression(o =>
{
    o.EnableForHttps = true;
    o.Providers.Add<BrotliCompressionProvider>();
    o.Providers.Add<GzipCompressionProvider>();
    o.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
    {
        "image/svg+xml", "application/javascript", "text/javascript", "application/json"
    });
});
builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Optimal);
builder.Services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Optimal);

// --- Large upload limits (Kestrel / IIS / form) ---
builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = maxUploadBytes;
    o.ValueLengthLimit = int.MaxValue;
    o.MultipartHeadersLengthLimit = int.MaxValue;
});
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = maxUploadBytes);
builder.Services.Configure<IISServerOptions>(o => o.MaxRequestBodySize = maxUploadBytes);

// --- Health checks (SQL + storage) ---
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("sql-server")
    .AddCheck<StorageHealthCheck>("storage");

var app = builder.Build();

// Ensure the storage folder structure exists before anything serves from it.
StorageInitializer.EnsureFolders(app.Services);

// elFinder working directories live outside the storage root (connector requirement).
Directory.CreateDirectory(ElFinderPaths.Thumb(app.Environment.ContentRootPath));

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.UseResponseCompression();

// App's own static assets (wwwroot).
app.UseStaticFiles();

// CDN: serve the shared storage volume with long-lived cache headers (ETag/Last-Modified are automatic).
var storage = app.Services.GetRequiredService<IOptions<StorageOptions>>().Value;
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(storage.RootPath),
    RequestPath = storage.CdnRequestPath,
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers[HeaderNames.CacheControl] =
            $"public,max-age={storage.CacheMaxAgeSeconds}";
    }
});

// elFinder-generated thumbnails (served from the working directory outside the storage root).
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(ElFinderPaths.Thumb(app.Environment.ContentRootPath)),
    RequestPath = ElFinderPaths.ThumbRequestPath
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapControllers(); // attribute-routed API + elFinder connector
app.MapHealthChecks("/health");

// Apply migrations and seed the admin user.
await DbSeeder.MigrateAndSeedAsync(app.Services);

app.Run();
