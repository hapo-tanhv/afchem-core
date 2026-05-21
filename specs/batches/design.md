# Thiết kế Kỹ thuật: Module Quản lý Mẻ (Batches) & Tích hợp HTTP API

Tài liệu này trình bày thiết kế chi tiết cho việc triển khai tính năng Quản lý mẻ (`batches`) trên nền tảng .NET Framework 4.5 của ứng dụng `HinoTools.Alarm`.

---

## 1. Kiến trúc Hệ thống và Luồng dữ liệu (Architecture & Data Flow)

Hệ thống sẽ được bổ sung một luồng thu nhận thông tin mẻ song song với luồng SCADA hiện tại.

```
                  ┌──────────────────────────────────────────────┐
                  │                 BÊN THỨ BA                   │
                  └──────────────────────┬───────────────────────┘
                                         │ HTTP POST (Port 5500)
                                         ▼
┌────────────────────────────────────────────────────────────────────────┐
│                        HinoTools.Alarm Server                          │
│                                                                        │
│  ┌───────────────────────┐                    ┌─────────────────────┐  │
│  │   BatchesHttpServer   │                    │  AlarmReportLogger  │  │
│  │ (Self-hosted HTTP API) │                    │   (State Machine)   │  │
│  └───────────┬───────────┘                    └──────────┬──────────┘  │
│              │                                           │             │
│              │ Insert Pending Batch                      │ Query Active│
│              │                                           │ Batch ID    │
│              ▼                                           ▼             │
│  ┌──────────────────────────────────────────────────────────────────┐  │
│  │                            MySQL DB                              │  │
│  │                                                                  │  │
│  │    [batches] <────────────── [alarmreport] ─── [alarmlog]        │  │
│  │  (Id, Name, Device,...)        (batchId)        (batchId)        │  │
│  └──────────────────────────────────────────────────────────────────┘  │
└────────────────────────────────────────────────────────────────────────┘
```

### Luồng nghiệp vụ:
1. **Bên thứ ba** gọi API tạo mẻ mới (`POST /api/batches/create` kèm `device_name`).
2. **BatchesHttpServer** tiếp nhận yêu cầu, sinh số thứ tự (stt) và tên mẻ theo định dạng `device_name-yyyyMMdd-stt`, sau đó lưu vào bảng `batches` với trạng thái `Pending`.
3. **AlarmReportLogger** trong khi giám sát:
   - Khi phát hiện `ThoiGianCapLieu > 0` và hệ thống đang Idle:
     - Tách tên thiết bị động (ví dụ từ `AFChemTX01.ThoiGianCapLieu` -> `TX01`).
     - Truy vấn DB lấy mẻ `Pending` cũ nhất (FIFO) của thiết bị này.
     - Cập nhật trạng thái mẻ thành `Active` và ghi nhận `start_time = DateTime.Now`.
     - Lưu ID mẻ này vào bộ nhớ làm mẻ hiện hành đang ghi log.
     - Nếu không có mẻ `Pending`, tự động sinh một mẻ khẩn cấp/fallback để tiếp tục ghi log mà không gây lỗi.
   - Định kỳ 30s ghi dữ liệu vào bảng `alarmreport` kèm theo `batchId` hiện hành.
   - Khi mẻ kết thúc (`ThoiGianXaHang == 0` và `ThoiGianRungXaHang == 0`):
     - Cập nhật trạng thái mẻ thành `Completed` và gán `end_time = DateTime.Now`.
     - Giải phóng mẻ hiện hành.
4. **AlarmLogger** khi nhận sự kiện cảnh báo sự cố từ `AlarmServer`:
   - Tách tên thiết bị tương ứng từ TagName của sự cố.
   - Truy vấn DB tìm mẻ đang ở trạng thái `Active` của thiết bị đó.
   - Nếu tồn tại, lưu cảnh báo vào bảng `alarmlog` kèm theo `batchId` tương ứng.

---

## 2. Thiết kế Cơ sở dữ liệu (Database Design)

### 2.1. Cấu trúc bảng `batches`
```sql
CREATE TABLE IF NOT EXISTS `batches` (
  `id` INT AUTO_INCREMENT PRIMARY KEY,
  `name` VARCHAR(100) NOT NULL UNIQUE,
  `device_name` VARCHAR(100) NOT NULL,
  `status` VARCHAR(50) NOT NULL DEFAULT 'Pending',
  `start_time` DATETIME NULL,
  `end_time` DATETIME NULL,
  `created_at` TIMESTAMP DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
```

### 2.2. Nâng cấp các bảng hiện có (Auto-Migration)
Trong cả hai class `AlarmReportLogger` (nằm trong `HinoTools.Data/Log/`) và `AlarmLogger` (nằm trong `HinoTools.Alarm/Control/`), tại phương thức khởi tạo bảng, chúng ta sẽ bổ sung logic kiểm tra cột `batchId` và nâng cấp bảng:

```csharp
// Kiểm tra và bổ sung cột batchId vào bảng
private void AddBatchIdColumnIfNeeded(string tableName)
{
    try
    {
        // Kiểm tra xem cột batchId đã tồn tại chưa
        string checkQuery = $"SHOW COLUMNS FROM `{tableName}` LIKE 'batchId'";
        var result = dataAccess.ExecuteScalarQuery(checkQuery);
        if (result == null || result == DBNull.Value)
        {
            // Nếu chưa có, tiến hành chèn thêm cột batchId dạng INT NULL
            string alterQuery = $"ALTER TABLE `{tableName}` ADD COLUMN `batchId` INT NULL DEFAULT NULL";
            dataAccess.ExecuteNonQuery(alterQuery);
            System.Diagnostics.Debug.WriteLine($"[Migration] Added batchId column to {tableName} successfully.");
        }
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"[Migration] ERROR adding batchId column to {tableName}: {ex.Message}");
    }
}
```

---

## 3. Thiết kế HTTP API Server tự lưu trữ (`BatchesHttpServer`)

Một lớp mới có tên `BatchesHttpServer` sẽ được tạo trong dự án `HinoTools.Data` (hoặc `HinoTools.Alarm`) để lắng nghe các HTTP Request trên cổng `5500`.

### 3.1. Định nghĩa Lớp `BatchesHttpServer`
```csharp
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HinoTools.Data.Database;

namespace HinoTools.Data.Http
{
    public class BatchesHttpServer
    {
        private HttpListener listener;
        private string connectionString;
        private bool isRunning;
        private const string DefaultDevice = "TX01";

        public BatchesHttpServer(string connectionString, int port = 5500)
        {
            this.connectionString = connectionString;
            listener = new HttpListener();
            listener.Prefixes.Add($"http://*:{port}/"); // Lắng nghe từ tất cả các IP
        }

        public void Start()
        {
            if (isRunning) return;
            isRunning = true;
            listener.Start();
            Task.Run(() => ListenLoop());
        }

        public void Stop()
        {
            isRunning = false;
            listener.Stop();
        }

        private async Task ListenLoop()
        {
            while (isRunning)
            {
                try
                {
                    var context = await listener.GetContextAsync();
                    ProcessRequest(context);
                }
                catch { }
            }
        }

        private void ProcessRequest(HttpListenerContext context)
        {
            // Logic định tuyến yêu cầu và xử lý POST /api/batches/create
        }
    }
}
```

### 3.2. Logic sinh Tên Mẻ và Đảm bảo Tránh Trùng lặp
Để sinh `stt` chính xác trong ngày:
```sql
SELECT COUNT(*) FROM `batches` 
WHERE `device_name` = @device_name 
  AND DATE(`created_at`) = CURDATE()
```
Hoặc:
```sql
SELECT IFNULL(MAX(CAST(SUBSTRING_INDEX(`name`, '-', -1) AS UNSIGNED)), 0)
FROM `batches`
WHERE `device_name` = @device_name 
  AND `name` LIKE @pattern
```
Tên mẻ kế tiếp sẽ được sinh bằng cách lấy giá trị cực đại cộng thêm 1, đảm bảo định dạng số thứ tự 2 chữ số (ví dụ: `01`, `02`).

---

## 4. Tách Device Name Động (Dynamic DeviceName)

Tất cả các thành phần ghi log đều kế thừa một phương thức tách tên thiết bị từ tag cấu hình đầu tiên:

```csharp
public static class DeviceNameHelper
{
    public static string ExtractDeviceName(string firstTagName)
    {
        if (string.IsNullOrEmpty(firstTagName)) return "TX01";
        
        // Trích xuất phần trước dấu chấm "."
        var dotIndex = firstTagName.IndexOf('.');
        string prefix = dotIndex > 0 ? firstTagName.Substring(0, dotIndex) : firstTagName;
        
        // Loại bỏ phần "AFChem" nếu tồn tại
        if (prefix.StartsWith("AFChem", StringComparison.OrdinalIgnoreCase) && prefix.Length > 6)
        {
            return prefix.Substring(6); // ví dụ: AFChemTX01 -> TX01, AFChemTX01 -> PLC
        }
        
        return prefix;
    }
}
```

---

## 5. Kế hoạch Tích hợp vào Ứng dụng Hiện tại

1. **Khởi chạy HTTP API Server**:
   - HTTP API Server sẽ được khởi tạo và chạy cùng lúc với `AlarmReportLogger` hoặc trong các ứng dụng Host (`TestServer`, `WindowsFormsApp1`).
   - Để tối đa hóa tính đóng gói, `AlarmReportLogger` sẽ tự động khởi chạy và tắt `BatchesHttpServer` khi component này được kích hoạt (`TryInitialize`). Điều này giúp người dùng chỉ cần kéo thả `AlarmReportLogger` trên Designer là API tự động được kích hoạt trên cổng `5500`.

2. **Tích hợp State Machine trong `AlarmReportLogger`**:
   - Bổ sung trường dữ liệu `activeBatchId` kiểu `int?` vào `AlarmReportLogger`.
   - Trong quá trình từ `Idle` sang `Logging` (khi `ThoiGianCapLieu > 0`):
     - Gọi hàm lấy mẻ `Pending` cũ nhất từ DB ứng với thiết bị hiện hành.
     - Cập nhật trạng thái của mẻ đó thành `Active`, lưu ID vào `activeBatchId`.
     - Nếu không tìm thấy, tự động tạo mới mẻ có trạng thái `Active`.
   - Trong quá trình ghi log liên tục:
     - Ghi nhận `batchId` bằng giá trị `activeBatchId` khi chạy câu lệnh INSERT vào `alarmreport`.
   - Khi mẻ kết thúc:
     - Cập nhật trạng thái mẻ đang hoạt động thành `Completed` kèm `end_time = DateTime.Now`.
     - Đặt lại `activeBatchId = null`.
