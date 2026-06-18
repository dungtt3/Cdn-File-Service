# Tích hợp Shared CDN & File Manager

Hướng dẫn cho các ứng dụng khác (ASP.NET MVC 5/Core, ReactJS, mobile, …) sử dụng dịch vụ
**Cdn-File-Service** để dùng chung tài nguyên tĩnh (js, css, images, fonts, documents, media).

> Dịch vụ đang chạy tại: **`https://cdn.staxi.vn`**

---

## 0. Các endpoint

| Mục đích | Đường dẫn | Xác thực |
|----------|-----------|----------|
| Tài nguyên tĩnh (đọc) | `https://cdn.staxi.vn/cdn/{folder}/{path}` | Không |
| REST API | `https://cdn.staxi.vn/api/files…` | Cookie (đăng nhập) |
| File Manager (UI elFinder) | `https://cdn.staxi.vn/FileManager` | Cookie |
| Đăng nhập | `https://cdn.staxi.vn/Account/Login` | — |
| Health check | `https://cdn.staxi.vn/health` | Không |

**Thư mục gốc:** `js`, `css`, `images`, `documents`, `fonts`, `media`, `temp` (cho phép tạo thư mục con).

Ví dụ URL tài nguyên thật:
```
https://cdn.staxi.vn/cdn/js/common.js
https://cdn.staxi.vn/cdn/css/common.css
https://cdn.staxi.vn/cdn/images/logo/logo.png
https://cdn.staxi.vn/cdn/images/logo/logo.webp        (bản WebP tự sinh)
https://cdn.staxi.vn/cdn/images/logo/logo_thumb.webp  (thumbnail tự sinh)
```

> **Lưu ý prefix `/cdn`:** file tĩnh được phục vụ dưới prefix `/cdn` (cấu hình `Storage:CdnRequestPath`).
> URL do API trả về (`cdnUrl`) được ghép từ `Storage:CdnBaseUrl`. Để URL khớp đường dẫn thật,
> đặt `CdnBaseUrl = https://cdn.staxi.vn/cdn` trên server. Nếu chỉ đọc tài nguyên, cứ dùng mẫu
> `https://cdn.staxi.vn/cdn/...` ở trên là chắc chắn đúng.

Tài nguyên tĩnh được trả kèm: `Cache-Control: public,max-age=31536000`, `ETag`, `Last-Modified`,
nén `gzip`/`brotli`. **Không cần xác thực để đọc.**

---

## 1. Sử dụng tài nguyên tĩnh (đọc)

Đây là cách dùng phổ biến nhất — chỉ cần tham chiếu URL.

### HTML / mọi framework
```html
<link href="https://cdn.staxi.vn/cdn/css/common.css" rel="stylesheet" />
<script src="https://cdn.staxi.vn/cdn/js/common.js"></script>
<img src="https://cdn.staxi.vn/cdn/images/logo/logo.png" alt="logo" />
```

### ASP.NET MVC 5 (.NET Framework) — gọn gàng qua helper
1. Thêm vào `Web.config` (hoặc `Configuration/User.config`):
   ```xml
   <add key="CDN_BASE_URL" value="https://cdn.staxi.vn/cdn" />
   ```
2. Helper:
   ```csharp
   public static class CdnHelper
   {
       public static string Cdn(this UrlHelper _, string path) =>
           ConfigurationManager.AppSettings["CDN_BASE_URL"].TrimEnd('/') + "/" + path.TrimStart('/');
   }
   ```
3. View:
   ```html
   <link href="@Url.Cdn("css/common.css")" rel="stylesheet" />
   <img src="@Url.Cdn("images/logo/logo.png")" />
   ```

### ReactJS / SPA
```js
const CDN = "https://cdn.staxi.vn/cdn";
<img src={`${CDN}/images/logo/logo.png`} />
```

### Đa công ty (multi-tenant)
Quy ước thư mục theo mã công ty để tách tài nguyên:
```
https://cdn.staxi.vn/cdn/images/company/19/banner.png
```

---

## 2. Upload tài nguyên từ server (REST API)

API dùng **Cookie Authentication** + **Claims** (`FileManager.Upload`, `FileManager.Delete`, …).
Client phía server đăng nhập một lần để lấy cookie rồi tái sử dụng.

> **Khuyến nghị:** tạo **tài khoản dịch vụ** riêng trên CDN (ví dụ `svc-webadmin`) và chỉ cấp
> quyền cần thiết, thay vì dùng `admin`.

### API reference
| Method | Route | Quyền |
|--------|-------|-------|
| GET | `/api/files?folder=&search=&page=&pageSize=` | `FileManager.View` |
| GET | `/api/files/{id}` | `FileManager.View` |
| POST | `/api/files/upload` (multipart: `file`, `folder`, `subPath?`) | `FileManager.Upload` |
| DELETE | `/api/files/{id}` | `FileManager.Delete` |
| GET | `/api/files/download/{id}` | `FileManager.Download` |

Kết quả upload (JSON) chứa `cdnUrl`, `wasDuplicate`, `wasNewVersion`, `file.id`, `relativePath`…

### Client C# (.NET Framework 4.5+) — tự đăng nhập & tái dùng phiên
```csharp
public class CdnFileServiceClient
{
    private readonly string _baseUrl;
    private readonly string _user, _pass;
    private readonly HttpClient _http;
    private readonly System.Threading.SemaphoreSlim _lock = new System.Threading.SemaphoreSlim(1, 1);
    private bool _loggedIn;

    public CdnFileServiceClient(string baseUrl, string user, string pass)
    {
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; // .NET 4.5.x cần TLS 1.2
        _baseUrl = baseUrl.TrimEnd('/'); _user = user; _pass = pass;
        var h = new HttpClientHandler { CookieContainer = new CookieContainer(), UseCookies = true, AllowAutoRedirect = true };
        _http = new HttpClient(h) { Timeout = TimeSpan.FromMinutes(10) };
    }

    public async Task<string> UploadAsync(byte[] content, string fileName, string folder,
        string subPath = null, string contentType = "application/octet-stream")
    {
        await EnsureLoginAsync();
        using (var form = new MultipartFormDataContent())
        {
            var fc = new ByteArrayContent(content);
            fc.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(contentType);
            form.Add(fc, "file", fileName);
            form.Add(new StringContent(folder), "folder");
            if (!string.IsNullOrEmpty(subPath)) form.Add(new StringContent(subPath), "subPath");

            var resp = await _http.PostAsync(_baseUrl + "/api/files/upload", form);
            if (resp.StatusCode == HttpStatusCode.Unauthorized || resp.StatusCode == HttpStatusCode.Found)
            { _loggedIn = false; await EnsureLoginAsync(); resp = await _http.PostAsync(_baseUrl + "/api/files/upload", form); }
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadAsStringAsync(); // JSON, có cdnUrl
        }
    }

    private async Task EnsureLoginAsync()
    {
        if (_loggedIn) return;
        await _lock.WaitAsync();
        try
        {
            if (_loggedIn) return;
            var html = await _http.GetStringAsync(_baseUrl + "/Account/Login");
            var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string,string>("UserName", _user),
                new KeyValuePair<string,string>("Password", _pass),
                new KeyValuePair<string,string>("__RequestVerificationToken", token),
            });
            var resp = await _http.PostAsync(_baseUrl + "/Account/Login", form);
            if (!resp.IsSuccessStatusCode && resp.StatusCode != HttpStatusCode.Found)
                throw new Exception("CDN login failed: " + resp.StatusCode);
            _loggedIn = true;
        }
        finally { _lock.Release(); }
    }
}
```
Dùng:
```csharp
var cdn = new CdnFileServiceClient("https://cdn.staxi.vn", "svc-webadmin", "****");
var json = await cdn.UploadAsync(bytes, "logo.png", folder: "images", subPath: "company/19", contentType: "image/png");
// json.cdnUrl -> lưu DB để hiển thị
```

> **File lớn:** API cho tới ~1 GB. Nếu upload **đi qua** một web app trung gian, chú ý giới hạn
> `maxAllowedContentLength` của app đó. Tốt nhất cho **trình duyệt upload thẳng** lên File Manager.

---

## 3. File Manager (elFinder)

### Liên kết (khuyến nghị)
```html
<a href="https://cdn.staxi.vn/FileManager" target="_blank" rel="noopener">Quản lý tài nguyên (CDN)</a>
```
Người dùng đăng nhập CDN (tài khoản riêng) rồi upload/đổi tên/di chuyển/copy/xoá/tìm kiếm/kéo–thả.

### Nhúng iframe
```html
<iframe src="https://cdn.staxi.vn/FileManager" style="width:100%;height:720px;border:0"></iframe>
```
(Vẫn cần đăng nhập CDN trong iframe; cần CDN không chặn nhúng từ origin của bạn.)

---

## 4. Tính năng tự động khi upload
- **Khử trùng lặp (SHA-256):** file trùng nội dung không lưu lại bản vật lý thứ hai.
- **Versioning:** upload đè cùng đường dẫn → giữ bản cũ trong `.versions/`, URL canonical không đổi.
- **Ảnh:** tự sinh `<tên>.webp` và `<tên>_thumb.webp`.

## 5. Bảo mật
- **Whitelist:** `js, css, png, jpg, jpeg, gif, svg, webp, pdf, docx, xlsx, zip`.
- **Blacklist:** `exe, bat, cmd, ps1, dll, msi`.
- Kiểm tra MIME + giới hạn dung lượng.

---

## 6. Checklist tích hợp
- [ ] Đọc tài nguyên: dùng `https://cdn.staxi.vn/cdn/{folder}/{path}` (không cần auth).
- [ ] Upload qua API: tạo **tài khoản dịch vụ** trên CDN + dùng `CdnFileServiceClient`.
- [ ] Quản lý thủ công: link/iframe tới `/FileManager`.
- [ ] App của bạn kết nối được tới `cdn.staxi.vn` (firewall/DNS/HTTPS).

## 7. Xử lý sự cố nhanh
| Triệu chứng | Nguyên nhân / xử lý |
|-------------|---------------------|
| Tài nguyên 404 | Thiếu/thừa prefix `/cdn`. Dùng `https://cdn.staxi.vn/cdn/...`. |
| Upload trả 302/401 | Sai tài khoản hoặc thiếu quyền `FileManager.Upload`. |
| Lỗi TLS khi gọi HTTPS từ .NET 4.5.x | Bật `ServicePointManager.SecurityProtocol \|= SecurityProtocolType.Tls12` (đã có trong client). |
| Extension bị từ chối | Nằm trong blacklist hoặc ngoài whitelist (mục 5). |
| Ảnh chưa có `.webp` | Job sinh thumbnail chạy định kỳ; hoặc ảnh lỗi giải mã (xem log CDN). |
