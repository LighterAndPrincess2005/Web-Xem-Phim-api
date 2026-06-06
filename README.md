# 🎬 LVDKMovie — Hướng dẫn cài đặt A→Z
cd D:\Downloads\LVDKMovie
dotnet run --project .\LVDKMovie\LVDKMovie.csproj --urls "http://0.0.0.0:5000"
192.168.1.8:5000

## YÊU CẦU HỆ THỐNG

| Thứ cần cài | Link tải |
|---|---|
| .NET 8 SDK | https://dotnet.microsoft.com/download/dotnet/8.0 |
| Git (tuỳ chọn) | https://git-scm.com |
| VS Code hoặc Visual Studio 2022 | https://code.visualstudio.com |

---

## BƯỚC 1 — Tải .NET 8 SDK

1. Vào https://dotnet.microsoft.com/download/dotnet/8.0
2. Chọn **SDK 8.x.x** → tải bản phù hợp với máy (Windows x64 phổ biến nhất)
3. Cài đặt như phần mềm bình thường, nhấn Next liên tục
4. Kiểm tra sau khi cài xong:
```
dotnet --version
```
Nếu hiện `8.x.x` là thành công ✅

---

## BƯỚC 2 — Giải nén project

1. Giải nén file `LVDKMovie.zip` ra thư mục bất kỳ, ví dụ: `C:\Projects\LVDKMovie`
2. Cấu trúc thư mục sẽ trông như sau:
```
LVDKMovie/
├── Controllers/
│   ├── HomeController.cs
│   └── MovieController.cs
├── Data/
│   └── AppDbContext.cs
├── Models/
│   └── OPhimModels.cs
├── Views/
│   ├── Home/
│   │   ├── Index.cshtml
│   │   ├── Search.cshtml
│   │   └── List.cshtml
│   ├── Movie/
│   │   ├── Detail.cshtml
│   │   └── Watch.cshtml
│   └── Shared/
│       └── _Layout.cshtml
├── Program.cs
├── appsettings.json
└── LVDKMovie.csproj
```

---

## BƯỚC 3 — Chạy project

### Cách 1: Dùng Terminal / Command Prompt (Nhanh nhất)

```bash
# Mở CMD hoặc PowerShell, vào thư mục project
cd D:\Downloads\LVDKMovie\LVDKMovie

# Khôi phục các thư viện (chỉ cần làm 1 lần đầu)
dotnet restore

# Chạy project
dotnet run
```

Neu dang dung o thu muc ngoai `D:\Downloads\LVDKMovie`, chay lenh nay de khoi loi "Couldn't find a project to run":
```bash
dotnet run --project .\LVDKMovie\LVDKMovie.csproj --urls "http://0.0.0.0:5000"
```

Sau vài giây sẽ thấy:
```
Now listening on: http://localhost:5000
```

Mở trình duyệt vào **http://localhost:5000** là xong! 🎉

---

### Chay cho dien thoai / may khac cung Wi-Fi

Cach nhanh nhat: bam double-click file:
```bat
RunMovie.bat
```

Hoac chay bang lenh:
```bash
cd D:\Downloads\LVDKMovie\LVDKMovie
dotnet run --urls "http://0.0.0.0:5000"
```

Mo tren may dang chay app:
```text
http://localhost:5000
```

Mo tren dien thoai / laptop khac cung Wi-Fi:
```text
http://IP-MAY-CHAY-APP:5000
```

Vi du neu may chay app co IP `192.168.1.8` thi mo:
```text
http://192.168.1.8:5000
```

Xem IP nhanh tren Windows:
```bat
ipconfig
```

Tim dong `IPv4 Address`, lay IP do thay vao link ben tren. Neu may khac khong vao duoc, chay `RunMovie.bat` bang quyen Administrator de tu them rule firewall cho port `5000`.

---

### Cách 2: Dùng Visual Studio 2022

1. Mở Visual Studio 2022
2. Chọn **Open a project or solution**
3. Dẫn đến file `LVDKMovie.csproj` → Open
4. Nhấn nút **▶ Run** (hoặc F5)
5. Trình duyệt tự động mở

---

### Cách 3: Dùng VS Code

1. Mở VS Code
2. File → Open Folder → chọn thư mục `LVDKMovie`
3. Mở Terminal trong VS Code (Ctrl + `)
4. Gõ:
```bash
dotnet run
```

---

## BƯỚC 4 — Database tự động

File **lvdkmovie.db** (SQLite) sẽ được tự tạo trong thư mục project khi chạy lần đầu.

Lịch sử xem phim được lưu tự động mỗi khi bạn nhấn xem một tập phim.

---

## CÁC TRANG TRONG WEB

| URL | Mô tả |
|---|---|
| `/` | Trang chủ — phim mới + lịch sử xem |
| `/phim/{slug}` | Chi tiết phim + danh sách tập |
| `/xem/{slug}/{tap}` | Trang xem phim với iframe player |
| `/Home/Search?keyword=abc` | Tìm kiếm phim |
| `/Home/TheLoai/hanh-dong` | Lọc theo thể loại |
| `/Home/QuocGia/han-quoc` | Lọc theo quốc gia |

---

## XỬ LÝ LỖI THƯỜNG GẶP

### ❌ Lỗi: "dotnet: command not found"
→ Chưa cài .NET SDK hoặc chưa restart terminal. Cài lại từ Bước 1.

### ❌ Lỗi: port đã được dùng
→ Đổi port trong `launchSettings.json` hoặc dùng:
```bash
dotnet run --urls "http://localhost:5001"
```

### ❌ Trang chủ không hiện phim
→ API OPhim có thể bị giới hạn IP. Thử refresh lại hoặc dùng VPN.

### ❌ Lỗi "Could not find file lvdkmovie.db"
→ Chạy `dotnet run` từ đúng thư mục chứa `.csproj`.

---

## DEPLOY LÊN INTERNET (Tuỳ chọn)

### Deploy lên Railway (Miễn phí, dễ nhất)
1. Đăng ký tại https://railway.app
2. New Project → Deploy from GitHub
3. Push code lên GitHub rồi kết nối
4. Railway tự detect .NET và deploy

### Build file thực thi
```bash
dotnet publish -c Release -o ./publish
```
File output ở thư mục `./publish/`

---

## THÔNG TIN DỰ ÁN

- **Framework**: ASP.NET Core 8 MVC
- **Database**: SQLite (tự tạo, không cần cài)  
- **API**: OPhim1 (https://ophim1.com)
- **Style**: Tailwind CSS CDN + Custom CSS
- **Font**: Urbanist (Google Fonts)
- **Màu chủ đạo**: Nền đen `#000` + Neon `#DEFF9A`

---

> 💡 Tip: Bookmark http://localhost:5000 để dùng lại sau. Web chạy hoàn toàn offline ngoại trừ phần gọi API lấy danh sách phim.
