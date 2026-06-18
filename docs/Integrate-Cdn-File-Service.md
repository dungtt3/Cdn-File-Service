# Hướng dẫn tích hợp Shared CDN & File Manager vào BA.STaxi.WebAdmin

Tài liệu này hướng dẫn tích hợp dịch vụ **Cdn-File-Service** (mini-CDN + File Manager,
ASP.NET Core 8) vào hệ thống **BA.STaxi.WebAdmin** (ASP.NET MVC 5 / .NET Framework 4.5.1).

> Mã nguồn dịch vụ: `C:\Users\dungtt3\Documents\GitHub\Cdn-File-Service`
> Tài liệu gốc của dịch vụ: xem `README.md` và `docs/deployment-iis.md` trong repo đó.

Có 3 hình thức tích hợp, dùng riêng hoặc kết hợp:

| # | Kịch bản | Dùng khi nào |
|---|----------|--------------|
| 1 | **Tham chiếu tài nguyên tĩnh** từ CDN (js/css/images/fonts…) | Mọi view muốn dùng tài nguyên dùng chung |
| 2 | **Upload tài nguyên từ server** qua REST API | Khi WebAdmin cần đẩy file (ảnh, tài liệu) lên CDN bằng code |
| 3 | **Mở/nhúng File Manager** (elFinder) | Khi cần cho người dùng quản lý file thủ công |

---

## 0. Thông tin endpoint của dịch vụ CDN

Giả sử dịch vụ chạy tại host `https://cdn.company.vn`.

| Mục đích | Đường dẫn |
|----------|-----------|
| Tài nguyên tĩnh (công khai) | `https://cdn.company.vn/cdn/{folder}/{file}` |
| REST API | `https://cdn.company.vn/api/files…` |
| File Manager (UI) | `https://cdn.company.vn/FileManager` |
| Đăng nhập | `https://cdn.company.vn/Account/Login` |
| Health check | `https://cdn.company.vn/health` |

> **Lưu ý về prefix `/cdn`:** Ứng dụng phục vụ file tĩnh dưới prefix cấu hình `Storage:CdnRequestPath`
> (mặc định `/cdn`). Khi triển khai host CDN chuyên dụng, có thể map root của host thẳng vào thư
> mục Storage để bỏ prefix. Vì vậy hãy đặt biến `CDN_BASE_URL` (mục 1) là **phần URL đứng ngay
> trước các thư mục gốc** `js/ css/ images/ …`:
> - Host chuyên dụng, root → Storage: `CDN_BASE_URL = https://cdn.company.vn`
> - Dùng nguyên ứng dụng kèm prefix: `CDN_BASE_URL = https://cdn.company.vn/cdn`

Tài nguyên tĩnh được trả kèm `Cache-Control: public,max-age=31536000`, `ETag`, `Last-Modified`
và nén gzip/brotli — **không cần xác thực** để đọc.

---

## 1. Tham chiếu tài nguyên tĩnh từ CDN

### 1.1. Thêm cấu hình

Thêm key vào `BA.STaxi.Web/Configuration/User.config`:

```xml
<add key="CDN_BASE_URL" value="https://cdn.company.vn" />
```

### 1.2. Thêm property vào `Global.cs`

Theo đúng pattern hiện có trong `BA.STaxi.Web/Global.cs` (dùng `ConfigAppHelpers` +
`AppSettingUtility`):

```csharp
private static string _cdnBaseUrl;
public static string CdnBaseUrl
{
    get
    {
        _cdnBaseUrl = ConfigAppHelpers.TryGetString("CDN_BASE_URL") ??
                      AppSettingUtility.TryGetString("CDN_BASE_URL", "https://cdn.company.vn");
        return _cdnBaseUrl.TrimEnd('/');
    }
}
```

### 1.3. Thêm helper dựng URL

Tạo `BA.STaxi.Web/Helpers/CdnHelper.cs`:

```csharp
using System.Web;
using System.Web.Mvc;

namespace BA.STaxi.Web.Helpers
{
    public static class CdnHelper
    {
        /// <summary>Dựng URL tuyệt đối tới tài nguyên trên CDN, ví dụ Cdn("js/common.js").</summary>
        public static string Cdn(this UrlHelper _, string relativePath)
        {
            return Global.CdnBaseUrl + "/" + (relativePath ?? string.Empty).TrimStart('/');
        }

        public static IHtmlString CdnScript(this HtmlHelper _, string relativePath)
        {
            var url = Global.CdnBaseUrl + "/" + (relativePath ?? string.Empty).TrimStart('/');
            return new HtmlString($"<script src=\"{HttpUtility.HtmlAttributeEncode(url)}\"></script>");
        }
    }
}
```

### 1.4. Sử dụng trong View

**Razor (`.cshtml`):**

```html
<link href="@Url.Cdn("css/common.css")" rel="stylesheet" />
<img src="@Url.Cdn("images/logo/logo.png")" alt="logo" />
@Html.CdnScript("js/common.js")
```

**View PHP (`.php` qua PhpViewEngine)** — ghép chuỗi trực tiếp:

```html
<script src="https://cdn.company.vn/js/common.js"></script>
```

> Ảnh có thể dùng biến thể WebP/thumbnail do CDN tự sinh:
> `@Url.Cdn("images/logo/logo.webp")`, `@Url.Cdn("images/logo/logo_thumb.webp")`.

### 1.5. (Tuỳ chọn) Đa công ty (multi-tenant)

Nếu muốn tách tài nguyên theo công ty, đặt theo quy ước thư mục có `MA_CONG_TY`:

```csharp
// images/company/19/banner.png
var url = Url.Cdn($"images/company/{Global.CompanyIdDefault}/banner.png");
```

---

## 2. Upload tài nguyên từ server qua REST API

REST API của CDN dùng **Cookie Authentication** + **Claims** (quyền `FileManager.Upload`,
`FileManager.Delete`, `FileManager.Download`…). Client phía server cần đăng nhập một lần để lấy
cookie, sau đó tái sử dụng cho các request kế tiếp.

> **Khuyến nghị:** tạo một **tài khoản dịch vụ** riêng trên CDN (ví dụ `staxi-webadmin`) và chỉ
> cấp các quyền cần thiết, thay vì dùng tài khoản `admin`.

### 2.1. Lớp client (`BA.STaxi.Web/Helpers/CdnFileServiceClient.cs`)

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BA.STaxi.Web.Helpers
{
    /// <summary>
    /// Client gọi REST API của Cdn-File-Service. Tự đăng nhập (cookie) và tái sử dụng phiên.
    /// Đăng ký dạng singleton trong Autofac (xem mục 2.2).
    /// </summary>
    public class CdnFileServiceClient
    {
        private readonly string _baseUrl;
        private readonly string _userName;
        private readonly string _password;
        private readonly HttpClient _http;
        private readonly CookieContainer _cookies = new CookieContainer();
        private readonly SemaphoreSlimLite _loginLock = new SemaphoreSlimLite();
        private bool _loggedIn;

        public CdnFileServiceClient(string baseUrl, string userName, string password)
        {
            // .NET 4.5.1: bắt buộc bật TLS 1.2 để gọi HTTPS hiện đại
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            _baseUrl = baseUrl.TrimEnd('/');
            _userName = userName;
            _password = password;

            var handler = new HttpClientHandler
            {
                CookieContainer = _cookies,
                AllowAutoRedirect = true,
                UseCookies = true
            };
            _http = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(10) };
        }

        /// <summary>Upload 1 file. folder = js/css/images/documents/fonts/media; subPath tuỳ chọn.</summary>
        public async Task<string> UploadAsync(byte[] content, string fileName, string folder,
            string subPath = null, string contentType = "application/octet-stream")
        {
            await EnsureLoggedInAsync();

            using (var form = new MultipartFormDataContent())
            {
                var fileContent = new ByteArrayContent(content);
                fileContent.Headers.ContentType =
                    System.Net.Http.Headers.MediaTypeHeaderValue.Parse(contentType);
                form.Add(fileContent, "file", fileName);
                form.Add(new StringContent(folder), "folder");
                if (!string.IsNullOrEmpty(subPath))
                    form.Add(new StringContent(subPath), "subPath");

                var resp = await _http.PostAsync(_baseUrl + "/api/files/upload", form);
                if (resp.StatusCode == HttpStatusCode.Unauthorized ||
                    resp.StatusCode == HttpStatusCode.Found) // 302 -> login
                {
                    _loggedIn = false;
                    await EnsureLoggedInAsync();
                    resp = await _http.PostAsync(_baseUrl + "/api/files/upload", form);
                }
                resp.EnsureSuccessStatusCode();
                return await resp.Content.ReadAsStringAsync(); // JSON UploadResultDto (chứa cdnUrl)
            }
        }

        public async Task<string> ListAsync(string folder = null, string search = null)
        {
            await EnsureLoggedInAsync();
            var url = _baseUrl + "/api/files?page=1&pageSize=50";
            if (!string.IsNullOrEmpty(folder)) url += "&folder=" + Uri.EscapeDataString(folder);
            if (!string.IsNullOrEmpty(search)) url += "&search=" + Uri.EscapeDataString(search);
            return await _http.GetStringAsync(url);
        }

        public async Task DeleteAsync(int id)
        {
            await EnsureLoggedInAsync();
            var resp = await _http.DeleteAsync(_baseUrl + "/api/files/" + id);
            resp.EnsureSuccessStatusCode();
        }

        // --- đăng nhập (xử lý antiforgery token của trang /Account/Login) ---
        private async Task EnsureLoggedInAsync()
        {
            if (_loggedIn) return;
            await _loginLock.WaitAsync();
            try
            {
                if (_loggedIn) return;

                var loginHtml = await _http.GetStringAsync(_baseUrl + "/Account/Login");
                var token = Regex.Match(loginHtml,
                    "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;

                var form = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("UserName", _userName),
                    new KeyValuePair<string, string>("Password", _password),
                    new KeyValuePair<string, string>("__RequestVerificationToken", token)
                });

                var resp = await _http.PostAsync(_baseUrl + "/Account/Login", form);
                // Đăng nhập thành công sẽ redirect (302/200) và set cookie auth.
                if (!resp.IsSuccessStatusCode && resp.StatusCode != HttpStatusCode.Found)
                    throw new Exception("CDN login failed: " + resp.StatusCode);

                _loggedIn = true;
            }
            finally
            {
                _loginLock.Release();
            }
        }
    }

    // Khoá nhẹ tránh đăng nhập đồng thời nhiều lần (SemaphoreSlim cũng dùng được trên .NET 4.5.1).
    internal sealed class SemaphoreSlimLite
    {
        private readonly System.Threading.SemaphoreSlim _s = new System.Threading.SemaphoreSlim(1, 1);
        public Task WaitAsync() => _s.WaitAsync();
        public void Release() => _s.Release();
    }
}
```

### 2.2. Đăng ký trong Autofac

Thêm vào module phù hợp trong `BA.STaxi.Web/App_Start/Modules/` (ví dụ `ServiceModule`):

```csharp
builder.Register(c => new CdnFileServiceClient(
        Global.CdnBaseUrl,
        AppSettingUtility.TryGetString("CDN_API_USER", "staxi-webadmin"),
        AppSettingUtility.TryGetString("CDN_API_PASS", "")))
    .AsSelf()
    .SingleInstance();
```

Thêm cấu hình vào `User.config`:

```xml
<add key="CDN_API_USER" value="staxi-webadmin" />
<add key="CDN_API_PASS" value="****" />
```

### 2.3. Dùng trong Controller

```csharp
public class ArticleController : BaseController
{
    private readonly CdnFileServiceClient _cdn;
    public ArticleController(CdnFileServiceClient cdn) { _cdn = cdn; }

    [HttpPost]
    public async Task<ActionResult> UploadBanner(HttpPostedFileBase file)
    {
        using (var ms = new MemoryStream())
        {
            file.InputStream.CopyTo(ms);
            var json = await _cdn.UploadAsync(
                ms.ToArray(), file.FileName,
                folder: "images",
                subPath: $"company/{Global.CompanyIdDefault}",
                contentType: file.ContentType);
            // json chứa cdnUrl -> lưu vào DB để hiển thị sau
            return Content(json, "application/json");
        }
    }
}
```

> **Giới hạn dung lượng:** CDN cho phép tới ~1 GB. Nếu upload **đi qua** WebAdmin (browser → WebAdmin
> → CDN), nhớ giới hạn `maxAllowedContentLength` của WebAdmin (hiện 50 MB trong `Web.config`).
> Với file lớn nên cho **trình duyệt upload thẳng** lên File Manager (mục 3) để không qua WebAdmin.

---

## 3. Mở / nhúng File Manager (elFinder)

### 3.1. Cách đơn giản nhất — liên kết menu (khuyến nghị)

File Manager là một ứng dụng riêng có đăng nhập riêng. Thêm một mục menu mở tab mới:

```html
<a href="@Global.CdnBaseUrl/FileManager" target="_blank" rel="noopener">Quản lý tài nguyên (CDN)</a>
```

Người dùng đăng nhập vào CDN một lần (tài khoản riêng), sau đó dùng đầy đủ chức năng upload,
download, đổi tên, di chuyển, copy, xoá, tìm kiếm, cây thư mục, kéo–thả.

### 3.2. Nhúng bằng iframe (tuỳ chọn)

```html
<iframe src="https://cdn.company.vn/FileManager"
        style="width:100%;height:720px;border:0"></iframe>
```

Yêu cầu để iframe hoạt động:
- Dịch vụ CDN **không** chặn nhúng (mặc định ASP.NET Core không gắn `X-Frame-Options: DENY`; nếu
  có middleware chặn thì cần cho phép origin của WebAdmin).
- Người dùng vẫn cần đăng nhập CDN trong iframe (phiên cookie riêng).

### 3.3. Nhúng trực tiếp client elFinder vào WebAdmin (nâng cao)

Có thể tải client elFinder ngay trong trang WebAdmin và trỏ `url` tới connector của CDN
(`https://cdn.company.vn/el-finder/connector`). Tuy nhiên đây là gọi **cross-site** nên cần:
- Bật **CORS** trên CDN cho origin của WebAdmin (`AddCors` + `WithOrigins(...).AllowCredentials()`).
- Cookie auth dạng cross-site: cookie phải `SameSite=None; Secure` (bắt buộc HTTPS).
- Chia sẻ phiên đăng nhập giữa 2 site (SSO) — hiện chưa có.

→ Trong giai đoạn đầu **nên dùng 3.1 (link) hoặc 3.2 (iframe)**. Khi cần nhúng sâu, hãy bổ sung
CORS + SSO cho dịch vụ CDN (đây là thay đổi ở phía Cdn-File-Service, không phải WebAdmin).

---

## 4. Checklist triển khai

- [ ] Dịch vụ Cdn-File-Service đã chạy và `GET /health` trả `Healthy`.
- [ ] DNS/host `cdn.company.vn` trỏ đúng, có HTTPS.
- [ ] Đã thêm `CDN_BASE_URL` (và `CDN_API_USER`/`CDN_API_PASS` nếu dùng upload) vào `User.config`.
- [ ] Đã thêm property `Global.CdnBaseUrl` và `CdnHelper`.
- [ ] (Upload) Đã tạo **tài khoản dịch vụ** trên CDN với đúng quyền và đăng ký `CdnFileServiceClient`.
- [ ] WebAdmin (và các node trong cluster) có thể kết nối tới host CDN (firewall, internal IP).
- [ ] Đã đổi mật khẩu mặc định `admin/Admin@123` trên CDN.

---

## 5. Lưu ý & xử lý sự cố

| Triệu chứng | Nguyên nhân / cách xử lý |
|-------------|--------------------------|
| `UploadAsync` trả 302 liên tục | Sai tài khoản hoặc thiếu quyền `FileManager.Upload`. Kiểm tra claims tài khoản dịch vụ. |
| Lỗi TLS/SSL khi gọi HTTPS | .NET 4.5.1 mặc định không bật TLS 1.2 — đã xử lý bằng `ServicePointManager.SecurityProtocol` trong client. |
| File tĩnh trả 404 | Sai `CDN_BASE_URL` (thiếu/thừa prefix `/cdn`). Xem lại mục 0. |
| Ảnh không có bản `.webp` | File vừa upload; job sinh thumbnail chạy mỗi 5 phút, hoặc kiểm tra log CDN nếu ảnh lỗi giải mã. |
| Upload file lớn lỗi qua WebAdmin | `maxAllowedContentLength` của WebAdmin (50 MB). Cho trình duyệt upload thẳng lên File Manager. |
| Extension bị từ chối | CDN có whitelist (`js,css,png,jpg,jpeg,gif,svg,webp,pdf,docx,xlsx,zip`) và blacklist (`exe,bat,cmd,ps1,dll,msi`). |

---

## 6. Tóm tắt

- **Đọc tài nguyên:** chỉ cần `CDN_BASE_URL` + `@Url.Cdn(...)` trong view. Không cần xác thực.
- **Ghi tài nguyên (server):** dùng `CdnFileServiceClient` với tài khoản dịch vụ (cookie auth).
- **Quản lý thủ công:** link/iframe tới `/FileManager`.
- File lớn nên upload thẳng từ trình duyệt lên CDN để tránh giới hạn 50 MB của WebAdmin.
