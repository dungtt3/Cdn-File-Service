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
| Đăng nhập (super-admin) | `https://cdn.staxi.vn/Account/Login` | — |
| **SSO từ site công ty (không login lần 2)** | `https://cdn.staxi.vn/sso?token=…&returnUrl=/FileManager` | Token ký HMAC |
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

## Đa công ty (multi-tenant) & SSO

Có **2 vùng**:

| Vùng | Vị trí | URL | Quyền quản lý |
|------|--------|-----|----------------|
| **Shared** (dùng chung) | thư mục gốc `js/css/images/...` | `/cdn/js/common.js` | Chỉ **super-admin** ghi; mọi công ty **đọc** |
| **Riêng công ty** | `companies/{companyId}/...` | `/cdn/companies/19/images/logo.png` | Chỉ user của công ty đó |

- `companyId` = `MA_CONG_TY`. File công ty tải lên luôn nằm trong `companies/{companyId}/...` và **chỉ** user của công ty đó thấy/sửa/xoá (qua File Manager lẫn API).
- User công ty thấy vùng **Shared ở chế độ chỉ-đọc** (để tái dùng asset chung) + toàn quyền trên thư mục công ty mình; **không** thấy file của công ty khác.
- **Read URL vẫn công khai** (bản chất CDN). Việc cô lập áp dụng cho **quản lý** (upload/sửa/xoá/duyệt), không phải GET công khai một URL.

> Server đặt `CompanyId` từ phiên đăng nhập, **bỏ qua mọi `companyId` client gửi lên** → không thể giả mạo sang công ty khác.

---

## SSO: mở File Manager từ site công ty, không đăng nhập lần 2

Người dùng đã đăng nhập ở site công ty (vd BA.STaxi) bấm menu → mở thẳng File Manager của CDN, **không login lại**. Cơ chế: site công ty (giữ secret chung) tạo **token ký HMAC-SHA256** chứa `companyId`, user, quyền, hạn dùng; CDN xác thực và cấp phiên cookie **giới hạn theo công ty đó**.

### Cấu hình (2 phía)
- Trên **CDN** (`appsettings.Production.json`): `"Sso": { "Secret": "<chuỗi-bí-mật-chung>", "MaxAgeSeconds": 300 }`.
- Trên **site công ty**: cùng `Secret`.

### Tạo token ở site công ty (ASP.NET MVC 5, chỉ cần `HMACSHA256`)
```csharp
public static class CdnSso
{
    // Khớp định dạng token mà CDN xác thực: base64url(payloadJson).base64url(HMACSHA256(secret, payloadB64))
    public static string BuildUrl(string cdnBase, string secret, int companyId, string userName,
        string[] perms, string returnUrl = "/FileManager")
    {
        long exp = DateTimeOffset.UtcNow.AddMinutes(2).ToUnixTimeSeconds();
        string nonce = Guid.NewGuid().ToString("N");
        // Chú ý: key viết thường c,u,p,e,n; p là CSV các quyền FileManager.*
        string payload = "{\"c\":" + companyId + ",\"u\":\"" + userName + "\",\"p\":\""
            + string.Join(",", perms) + "\",\"e\":" + exp + ",\"n\":\"" + nonce + "\"}";
        string pB64 = B64Url(Encoding.UTF8.GetBytes(payload));
        using var h = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        string sig = B64Url(h.ComputeHash(Encoding.UTF8.GetBytes(pB64)));
        string token = pB64 + "." + sig;
        return cdnBase.TrimEnd('/') + "/sso?token=" + Uri.EscapeDataString(token)
            + "&returnUrl=" + Uri.EscapeDataString(returnUrl);
    }

    private static string B64Url(byte[] b) =>
        Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
```

### Menu trong site công ty
```csharp
// Controller
var url = CdnSso.BuildUrl(
    cdnBase: "https://cdn.staxi.vn",
    secret: Global.CdnSsoSecret,                 // đọc từ User.config
    companyId: Global.CompanyIdDefault,           // = MA_CONG_TY của user đang đăng nhập
    userName: User.Identity.Name,
    perms: new[] { "FileManager.View", "FileManager.Upload", "FileManager.Edit",
                   "FileManager.Delete", "FileManager.Download" });
ViewBag.CdnFileManagerUrl = url;
```
```html
<a href="@ViewBag.CdnFileManagerUrl" target="_blank" rel="noopener">Quản lý file (CDN)</a>
```

Người dùng bấm → CDN xác thực token → vào thẳng File Manager: thấy **Shared (read-only)** + **thư mục công ty mình (toàn quyền)**.

- Token nên có **hạn ngắn** (vài phút) — chỉ dùng để "vào cửa", phiên sau đó là cookie của CDN.
- SSO **không** cấp quyền super-admin; chỉ cấp các quyền `FileManager.*` ghi trong token.
- Token sai/hết hạn → CDN trả **401**.
- Mặc định mở bằng **link/tab mới** (cookie `SameSite=Lax`). Muốn **nhúng iframe** trong site công ty thì CDN phải đổi cookie sang `SameSite=None; Secure`.

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
