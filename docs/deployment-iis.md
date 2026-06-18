# IIS Deployment Guide

This guide deploys the **Shared CDN & File Manager** (`CdnFileService.Web`) to IIS.

## Prerequisites

- Windows Server with **IIS** enabled.
- **.NET 8 Hosting Bundle** installed (provides the ASP.NET Core Module v2). Download:
  <https://dotnet.microsoft.com/download/dotnet/8.0> → "Hosting Bundle". Restart IIS afterwards:
  ```powershell
  net stop was /y
  net start w3svc
  ```
- **SQL Server** reachable from the server (or SQL Express / LocalDB for a single box).

## 1. Configure

Edit `appsettings.json` (or set environment variables) before publishing:

- `ConnectionStrings:DefaultConnection` — your SQL Server.
- `Storage:RootPath` — absolute path to the shared storage volume, e.g. `D:\CdnStorage`.
  Use a path **outside** the site folder so deployments don't wipe assets. The IIS app-pool
  identity must have **Modify** permission on it.
- `Storage:CdnBaseUrl` — the public CDN host, e.g. `https://cdn.company.vn`.
- `SeedAdmin:Password` — change the default admin password.

## 2. Database

Apply the schema in one of two ways:

- **EF migrations (recommended):** the app runs `Database.Migrate()` automatically on startup,
  so the database/tables are created on first run (the app-pool login needs `db_ddladmin` the first time).
- **Manual script:** run [`database.sql`](database.sql) against a fresh `CdnFileService` database.

> The Serilog `Logs` table is created automatically at runtime by the MSSqlServer sink
> (`AutoCreateSqlTable = true`).

## 3. Publish

```powershell
dotnet publish src/CdnFileService.Web -c Release /p:PublishProfile=IISProfile
```

Output lands in `src/CdnFileService.Web/bin/Release/net8.0/publish`. The included
[`web.config`](../src/CdnFileService.Web/web.config) sets `maxAllowedContentLength` to 1 GB to
allow large uploads.

## 4. Create the IIS site

1. Copy the `publish` folder contents to e.g. `C:\inetpub\CdnFileService`.
2. Create an **Application Pool**: .NET CLR version = **No Managed Code**, identity =
   `ApplicationPoolIdentity` (or a domain account with access to SQL + the storage path).
3. Create a **Website** pointing at the folder, bound to your host name / HTTPS certificate.
4. Grant the app-pool identity **Modify** on `Storage:RootPath`.
5. Browse the site → sign in with the seeded admin account → change the password.

## 5. CDN host

Point `cdn.company.vn` (DNS + binding) at this site. Assets are served under the
`Storage:CdnRequestPath` prefix (default `/cdn`) with `Cache-Control: public,max-age=31536000`,
plus `ETag` / `Last-Modified` and gzip/brotli compression. Other applications reference assets as:

```html
<script src="https://cdn.company.vn/js/common.js"></script>
<link href="https://cdn.company.vn/css/common.css" rel="stylesheet" />
<img src="https://cdn.company.vn/images/logo/logo.png" />
```

## 6. Cluster (4 nodes)

Run the same site on each node and point `Storage:RootPath` at **shared storage** (UNC share /
SMB / clustered disk) so all nodes serve the same assets and metadata DB. Quartz uses an in-memory
store, so background jobs run independently per node — fine for the current cleanup/thumbnail/verify
jobs.

## Health check

`GET /health` returns `Healthy` when SQL Server and the storage folder are reachable/writable.
Use it for load-balancer probes.
