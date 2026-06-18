Bạn có thể dùng prompt/specification dưới đây cho AI (ChatGPT, Claude Code, Copilot Agent, Cursor Agent...) để tạo một dự án hoàn chỉnh.

---

# Yêu cầu xây dựng hệ thống Shared CDN & File Manager

## Bối cảnh

Tôi đang vận hành nhiều website ASP.NET MVC trên IIS Cluster (4 node).

Tôi cần xây dựng một website mới chuyên dùng để quản lý và phân phối tài nguyên dùng chung cho toàn bộ hệ thống:

* JavaScript
* CSS
* Images
* Documents
* Fonts
* Media files

Website này đóng vai trò:

```text
Shared Resource Center
+
CDN nội bộ
+
File Manager
```

Toàn bộ website khác trong hệ thống sẽ tham chiếu tài nguyên từ website này.

Ví dụ:

```html
<script src="https://cdn.company.vn/js/common.js"></script>

<link href="https://cdn.company.vn/css/common.css" rel="stylesheet">

<img src="https://cdn.company.vn/images/logo.png">
```

---

# Công nghệ bắt buộc

## Backend

* ASP.NET Core 8 Web Application
* C#
* Entity Framework Core
* SQL Server

## Frontend

* Bootstrap 5
* JQuery

## Authentication

* Cookie Authentication

## Authorization

* Claims Based Authorization

---

# Kiến trúc

Áp dụng Clean Architecture đơn giản:

```text
/src

Web
Application
Domain
Infrastructure
```

---

# Chức năng chính

## 1. File Manager

Tích hợp thư viện:

```text
elFinder
```

Không tự viết File Explorer từ đầu.

Yêu cầu:

* Upload file
* Download file
* Rename
* Move
* Copy
* Delete
* Search
* Folder tree
* Drag & Drop

---

## 2. Quản lý thư mục

Hệ thống phải tự động tạo cấu trúc:

```text
Storage

├── js
├── css
├── images
├── documents
├── fonts
├── media
└── temp
```

Cho phép tạo thêm thư mục con.

Ví dụ:

```text
images
 ├── logo
 ├── banner
 └── product
```

---

## 3. Metadata Database

Không chỉ lưu file vật lý.

Phải lưu metadata vào SQL Server.

Tạo bảng:

```sql
Files
```

Các cột:

```sql
Id
FileName
OriginalFileName
Extension
MimeType
Size
PhysicalPath
RelativePath
Folder
Hash
CreatedBy
CreatedDate
UpdatedDate
IsDeleted
```

---

## 4. File Version

Hỗ trợ version file.

Ví dụ:

```text
common.js
```

Upload mới:

```text
common.js
```

không ghi đè dữ liệu cũ.

Tạo:

```text
common_v2.js
```

hoặc

```text
FileVersion table
```

Cho phép rollback.

---

## 5. CDN URL

Tự sinh URL.

Ví dụ:

```text
https://cdn.company.vn/js/common.js
```

Hoặc:

```text
https://cdn.company.vn/images/logo/logo.png
```

---

## 6. Cache Control

Static files phải trả header:

```http
Cache-Control: public,max-age=31536000
```

ETag

Last-Modified

````

Bật Response Compression:

```text
gzip
brotli
````

---

## 7. Upload lớn

Hỗ trợ:

```text
100MB
500MB
1GB
```

Chunk Upload.

Resume Upload.

---

## 8. Duplicate Detection

Khi upload:

Tính SHA256.

Nếu file đã tồn tại:

```text
Không lưu lại
```

Chỉ tạo record metadata mới.

---

## 9. Image Processing

Tự động:

* Resize
* Thumbnail
* WebP

Ví dụ:

```text
logo.png
```

Sinh:

```text
logo.webp
logo_thumb.webp
```

Sử dụng:

```text
SixLabors.ImageSharp
```

---

## 10. Security

Whitelist extension:

```text
js
css
png
jpg
jpeg
gif
svg
webp
pdf
docx
xlsx
zip
```

Blacklist:

```text
exe
bat
cmd
ps1
dll
msi
```

Kiểm tra MimeType.

Kiểm tra kích thước.

---

## 11. Permission

Module:

```text
FileManager.View
FileManager.Upload
FileManager.Edit
FileManager.Delete
FileManager.Download
```

Sử dụng Claim-Based Authorization.

Ví dụ:

```csharp
[Authorize(Policy = "FileManager.Upload")]
```

---

## 12. Audit Log

Lưu:

```text
Upload
Delete
Rename
Move
Download
```

Thông tin:

```text
User
IP
Action
File
Time
```

---

## 13. API

Tạo REST API.

```http
GET /api/files

GET /api/files/{id}

POST /api/files/upload

DELETE /api/files/{id}

GET /api/files/download/{id}
```

---

## 14. Health Check

```http
/health
```

Kiểm tra:

* SQL Server
* Storage Folder

---

## 15. Background Jobs

Sử dụng:

```text
Quartz.NET
```

Job:

### Generate Thumbnail

```text
5 phút/lần
```

### Cleanup Temp

```text
1 ngày/lần
```

### Verify File Integrity

```text
1 ngày/lần
```

---

## 16. Deployment

Hỗ trợ IIS.

Publish Profile.

web.config.

README triển khai.

---

## 17. Logging

Sử dụng:

```text
Serilog
```

Sink:

```text
File
SQL Server
```

---

## 18. Tài liệu cần sinh

AI phải sinh:

### Database Script

```sql
CREATE TABLE ...
```

### Source Code hoàn chỉnh

### Folder Structure

### appsettings.json

### Dependency Injection

### Middleware

### Authorization Policies

### Quartz Jobs

### README.md

### Deployment Guide IIS

---

# Mục tiêu cuối cùng

Tạo một hệ thống tương đương:

* Mini CDN
* File Server
* Asset Management System

Có thể dùng chung cho nhiều website ASP.NET MVC, ASP.NET Core, ReactJS, Mobile App trong nội bộ doanh nghiệp mà không cần phụ thuộc AWS S3 hoặc MinIO ở giai đoạn đầu.
