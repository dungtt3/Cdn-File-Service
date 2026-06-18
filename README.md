# Cdn-File-Service

Internal **Shared CDN & File Manager** — a single ASP.NET Core 8 site that stores and serves shared
assets (JavaScript, CSS, images, documents, fonts, media) for many in-house applications, so they can
reference resources from one place:

```html
<script src="https://cdn.company.vn/js/common.js"></script>
<link href="https://cdn.company.vn/css/common.css" rel="stylesheet" />
<img src="https://cdn.company.vn/images/logo/logo.png" />
```

It is a mini-CDN + file server + asset-management system, usable by ASP.NET MVC / Core, ReactJS and
mobile apps without depending on AWS S3 or MinIO.

## Tech stack

- ASP.NET Core 8 (MVC + REST API), C#
- Entity Framework Core + SQL Server
- Bootstrap 5 + jQuery, **elFinder** file manager
- Cookie authentication + **claims-based** authorization
- Serilog (file + SQL Server), Quartz.NET background jobs, SixLabors.ImageSharp

## Architecture (Clean Architecture)

```
src/
  CdnFileService.Domain          entities (FileAsset, FileVersion, AuditLog, AppUser, UserClaim)
  CdnFileService.Application      interfaces, DTOs, permission constants, options
  CdnFileService.Infrastructure   EF Core, storage/image/audit/user services, Quartz jobs, Serilog
  CdnFileService.Web              MVC + API host, elFinder connector, auth, health, CDN middleware
docs/
  database.sql                    generated schema script
  deployment-iis.md               IIS deployment guide
```

## Features

| # | Feature | Notes |
|---|---------|-------|
| 1 | File Manager | elFinder connector at `/el-finder/connector` (upload, download, rename, move, copy, delete, search, folder tree, drag & drop) |
| 2 | Folder structure | `js, css, images, documents, fonts, media, temp` auto-created on startup; sub-folders supported |
| 3 | Metadata DB | `Files` table mirrors the spec; queried via REST API and dashboard |
| 4 | Versioning | Re-uploading a path keeps history under `.versions/`; canonical URL stays stable |
| 5 | CDN URLs | Generated from `Storage:CdnBaseUrl` |
| 6 | Cache control | `Cache-Control: public,max-age=31536000`, `ETag`, `Last-Modified`, gzip + brotli |
| 7 | Large uploads | Limits raised to ~1 GB (Kestrel/IIS/form). *Chunk resume is a planned enhancement.* |
| 8 | Duplicate detection | SHA-256; identical content is not re-stored, a metadata alias is created |
| 9 | Image processing | ImageSharp generates `<name>.webp` and `<name>_thumb.webp` |
| 10 | Security | Extension whitelist/blacklist + size + MIME checks |
| 11 | Permissions | Claims policies `FileManager.View/Upload/Edit/Delete/Download` |
| 12 | Audit log | Upload/Delete/Download recorded with user + IP |
| 13 | REST API | `/api/files` (see below) |
| 14 | Health check | `/health` verifies SQL + storage |
| 15 | Background jobs | Quartz: thumbnails (5 min), temp cleanup (daily), integrity verify (daily) |
| 16 | Deployment | `web.config`, publish profile, [IIS guide](docs/deployment-iis.md) |
| 17 | Logging | Serilog → rolling file + SQL `Logs` table |

## Run locally

Requires the .NET 8 SDK/runtime and a SQL Server (LocalDB is fine).

```bash
# 1. Configure the connection string in src/CdnFileService.Web/appsettings.json
#    (default: (localdb)\MSSQLLocalDB). The Development override targets (localdb)\ProjectModels.

# 2. Create the database (or let the app migrate on first run)
dotnet ef database update -p src/CdnFileService.Infrastructure -s src/CdnFileService.Web

# 3. Run
dotnet run --project src/CdnFileService.Web
```

Open the site, sign in with the seeded admin account, then **change the password**:

- Username: `admin`
- Password: `Admin@123`

## REST API

All endpoints require authentication; write actions require the matching permission claim.

| Method | Route | Policy |
|--------|-------|--------|
| GET | `/api/files?folder=&search=&page=&pageSize=` | `FileManager.View` |
| GET | `/api/files/{id}` | `FileManager.View` |
| POST | `/api/files/upload` (multipart: `file`, `folder`, `subPath?`) | `FileManager.Upload` |
| DELETE | `/api/files/{id}` | `FileManager.Delete` |
| GET | `/api/files/download/{id}` | `FileManager.Download` |

## Configuration (`appsettings.json`)

- `ConnectionStrings:DefaultConnection` — SQL Server connection.
- `Storage:RootPath` — storage root (relative paths resolve under the content root).
- `Storage:CdnBaseUrl` / `Storage:CdnRequestPath` — public host and local serve prefix.
- `Storage:AllowedExtensions` / `BlockedExtensions` / `MaxUploadSizeBytes`.
- `SeedAdmin:*` — initial admin user.

## Deployment

See [docs/deployment-iis.md](docs/deployment-iis.md) for IIS (including the 4-node cluster with
shared storage).

## Planned enhancements

- Chunked-upload **resume** (restart from offset).
- Fuller `GenerateThumbnail` / `VerifyFileIntegrity` job reporting.
- Vendoring elFinder client assets locally instead of referencing a CDN.
