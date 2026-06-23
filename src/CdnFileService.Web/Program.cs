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
using Microsoft.AspNetCore.DataProtection;
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

// --- Data Protection ---
// All instances must share the same key ring (and application name), otherwise the auth cookie
// encrypted on one backend cannot be decrypted on another behind the load balancer -> the user is
// repeatedly sent back to the login page. Set "DataProtection:KeysPath" to a shared folder/UNC path
// reachable by every CDN server. When unset (single-server/dev) the default local key ring is used.
var dpKeysPath = builder.Configuration["DataProtection:KeysPath"];
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("CdnFileService");
if (!string.IsNullOrWhiteSpace(dpKeysPath))
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(dpKeysPath));

// --- Authentication & claims-based authorization ---
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Account/Login";
        o.LogoutPath = "/Account/Logout";
        o.AccessDeniedPath = "/Account/Denied";
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.SlidingExpiration = true;
        // SameSite=None + Secure so the session cookie is accepted/sent when the File Manager is
        // embedded as a cross-site iframe inside a company site (e.g. BA.STaxi "Chọn ảnh").
        // Requires HTTPS (cdn.staxi.vn is HTTPS).
        o.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.None;
        o.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
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

// Framing policy: allow the File Manager to be embedded as an iframe by trusted company sites.
// Configure "Security:FrameAncestors" (e.g. [ "https://admin.staxi.vn", "http://localhost:18445" ]);
// when empty, framing is open ("*"). X-Frame-Options is removed so CSP frame-ancestors governs.
var frameAncestors = builder.Configuration.GetSection("Security:FrameAncestors").Get<string[]>();
var frameAncestorsCsp = (frameAncestors != null && frameAncestors.Length > 0)
    ? "frame-ancestors 'self' " + string.Join(" ", frameAncestors)
    : "frame-ancestors *";
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        context.Response.Headers.Remove("X-Frame-Options");
        context.Response.Headers["Content-Security-Policy"] = frameAncestorsCsp;
        return Task.CompletedTask;
    });
    await next();
});

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
        // Public CDN reads: allow cross-origin so @font-face fonts (and any CORS-checked
        // resources) referenced from other sites' stylesheets are not blocked by the browser.
        if (!string.IsNullOrEmpty(storage.CorsAllowOrigin))
        {
            ctx.Context.Response.Headers[HeaderNames.AccessControlAllowOrigin] = storage.CorsAllowOrigin;
            ctx.Context.Response.Headers[HeaderNames.Vary] = HeaderNames.Origin;
        }
    }
});

// elFinder-generated thumbnails (served from the working directory outside the storage root).
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(ElFinderPaths.Thumb(app.Environment.ContentRootPath)),
    RequestPath = ElFinderPaths.ThumbRequestPath
});

// CHIPS: mark SameSite=None cookies as Partitioned so the auth/session cookie still works when the
// File Manager is embedded as a cross-site iframe (company site) under browser third-party-cookie
// blocking. The cookie is partitioned by the embedding (top-level) site. Requires Secure.
app.UseCookiePolicy(new CookiePolicyOptions
{
    OnAppendCookie = ctx =>
    {
        if (ctx.CookieOptions.SameSite == Microsoft.AspNetCore.Http.SameSiteMode.None
            && !ctx.CookieOptions.Extensions.Contains("Partitioned"))
        {
            ctx.CookieOptions.Extensions.Add("Partitioned");
        }
    }
});

app.UseAuthentication();
app.UseAuthorization();

// File Manager is the main/landing screen (the Dashboard is super-admin only).
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=FileManager}/{action=Index}/{id?}");
app.MapControllers(); // attribute-routed API + elFinder connector
app.MapHealthChecks("/health");

// Apply migrations and seed the admin user.
await DbSeeder.MigrateAndSeedAsync(app.Services);

app.Run();
