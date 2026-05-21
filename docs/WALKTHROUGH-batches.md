# Tài liệu tổng kết: Module Quản lý Mẻ (Batches) và HTTP API
Tài liệu này tổng hợp toàn bộ các thay đổi, cấu trúc cơ sở dữ liệu, các API Endpoint và hướng dẫn kiểm thử cho tính năng Quản lý mẻ trộn (`batches`).

---

## 1. Các thành phần đã triển khai (Implemented Components)

Chúng tôi đã xây dựng và tích hợp thành công các thành phần sau:

### 1.1. Lớp Helper Tách Tên Thiết Bị Động (`DeviceNameHelper.cs`)
- **Đường dẫn**: `HinoTools.Data/Helper/DeviceNameHelper.cs`
- **Mục đích**: Tách động tên thiết bị từ tag name SCADA. Loại bỏ các tiền tố như `AFChem` và các hậu tố theo ký tự `.`.
- **Ví dụ**:
  - `AFChemTX01.ThoiGianCapLieu` ➔ `TX01`
  - `AFChemPLC` ➔ `PLC`
  - `AFChemTX02.NhietDo` ➔ `TX02`

### 1.2. Self-hosted HTTP API Server (`BatchesHttpServer.cs`)
- **Đường dẫn**: `HinoTools.Data/Http/BatchesHttpServer.cs`
- **Mục đích**: Chạy một server HTTP lắng nghe cổng `5500` độc lập để tiếp nhận yêu cầu từ bên thứ ba.
- **Tính năng**:
  - Hỗ trợ CORS tự động (cho phép các ứng dụng Web bên ngoài gọi API).
  - Tự động sinh mã số thứ tự `stt` và tên mẻ tăng dần trong ngày theo định dạng: `device_name-yyyyMMdd-stt` (đảm bảo không trùng lặp và an toàn đa luồng thông qua cơ chế lock DB).
  - Chèn mẻ trộn ở trạng thái `Pending` vào bảng `batches`.
  - Hỗ trợ phản hồi OPTIONS (Pre-flight requests) cho các cuộc gọi từ trình duyệt.

### 1.3. Cập nhật State Machine trong `AlarmReportLogger.cs`
- **Đường dẫn**: `HinoTools.Data/Log/AlarmReportLogger.cs`
- **Tính năng mới**:
  - **Khởi chạy HTTP Server**: Tự động tạo và chạy `BatchesHttpServer` trên cổng `HttpPort` (mặc định là `5500`) khi component được khởi tạo (`TryInitialize`). Tự động tắt server khi giải phóng component (`Dispose`).
  - **Auto-Migration**: Tự động tạo bảng `batches` nếu chưa tồn tại, và tự động kiểm tra, bổ sung cột `batchId` vào bảng `alarmreport`.
  - **Logic FIFO**: Khi phát hiện ThoiGianCapLieu > 0 (bắt đầu mẻ), tự động tìm kiếm mẻ ở trạng thái `Pending` cũ nhất của thiết bị đó để cập nhật thành `Active` và lưu ID mẻ vào bộ nhớ (`activeBatchId`). Nếu không có mẻ `Pending` nào, tự động tạo một mẻ khẩn cấp/fallback làm mẻ hoạt động.
  - **Liên kết Báo cáo**: Chèn ID mẻ hoạt động (`activeBatchId`) vào cột `batchId` khi ghi nhận dữ liệu định kỳ vào bảng `alarmreport`.
  - **Hoàn thành mẻ trộn**: Khi `ThoiGianXaHang == 0` và `ThoiGianRungXaHang == 0` (kết thúc Công đoạn 5), tự động cập nhật trạng thái mẻ trộn đó thành `Completed` và gán thời gian kết thúc `end_time = DateTime.Now`.

### 1.4. Cập nhật Báo cáo Cảnh báo Lỗi trong `AlarmLogger.cs`
- **Đường dẫn**: `HinoTools.Alarm/Control/AlarmLogger.cs`
- **Tính năng mới**:
  - **Auto-Migration**: Tự động kiểm tra và thêm cột `batchId` vào bảng `alarmlog`.
  - **Liên kết Cảnh báo**: Khi hệ thống ghi nhận một cảnh báo lỗi mới thông qua sự kiện `alarmHub.Pushed`, logger sẽ tách tên thiết bị động từ `TagName` của cảnh báo đó, truy vấn tìm mẻ trộn đang ở trạng thái `Active` hiện tại của thiết bị, và chèn ID mẻ vào cột `batchId` của bản ghi cảnh báo.

---

## 2. Thiết kế Cơ sở dữ liệu (Database Design)

### 2.1. Cấu trúc bảng `batches`
Bảng này quản lý thông tin toàn bộ mẻ trộn:
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

### 2.2. Nâng cấp các bảng `alarmreport` và `alarmlog`
Cả hai bảng đều được bổ sung tự động cột `batchId` dạng `INT NULL DEFAULT NULL` làm khóa ngoại mềm tham chiếu đến bảng `batches.id`.

---

## 3. Hướng dẫn sử dụng API (API Specification)

### 3.1. Yêu cầu tạo mẻ trộn mới
- **Endpoint**: `POST http://localhost:5500/api/batches/create`
- **Headers**: `Content-Type: application/json`
- **Body** (Tùy chọn):
```json
{
  "device_name": "TX01"
}
```
*Lưu ý: Nếu không gửi body hoặc không cung cấp `device_name`, API sẽ mặc định sử dụng thiết bị là `"TX01"`.*

- **Phản hồi Thành công (200 OK)**:
```json
{
  "success": true,
  "message": "Batch created successfully",
  "data": {
    "id": 15,
    "name": "TX01-20260520-01",
    "device_name": "TX01",
    "status": "Pending"
  }
}
```

- **Phản hồi Lỗi (500 Internal Server Error)**:
```json
{
  "success": false,
  "message": "Chi tiết thông báo lỗi..."
}
```

---

## 4. Kịch bản Kiểm thử & Xác minh (Verification Plan)

### 4.1. Chạy thử nghiệm tự động qua PowerShell
Chúng tôi đã chuẩn bị sẵn một script kiểm thử tại: `C:\Users\tanhv\.gemini\antigravity\brain\6ce710a2-aeea-45cc-a9c6-69fa2e04c80a\scratch\test_api.ps1`.

**Cách thực hiện**:
1. Khởi chạy ứng dụng SCADA Host (ví dụ: `WindowsFormsApp1.exe`). Khi Form chính mở lên, `AlarmReportLogger` sẽ tự động khởi động HTTP Server trên cổng 5500.
2. Mở PowerShell và chạy script kiểm thử:
   ```powershell
   & "C:\Users\tanhv\.gemini\antigravity\brain\6ce710a2-aeea-45cc-a9c6-69fa2e04c80a\scratch\test_api.ps1"
   ```
3. Script sẽ kiểm tra:
   - Tạo mẻ mặc định.
   - Tạo mẻ cho thiết bị `TX01`.
   - Tạo mẻ cho thiết bị `TX02` (xác minh tên mẻ sinh tăng dần tự động: `TX02-20260520-01`).
   - Kiểm tra OPTIONS Pre-flight request.

### 4.2. Kiểm tra chu kỳ hoạt động trong Database
Khi mô phỏng chạy SCADA PLC thực tế:
1. **Bước 1**: Gửi POST tạo mẻ trộn cho `TX01` ➔ Nhận về mẻ `TX01-20260520-01` (Trạng thái `Pending`).
2. **Bước 2**: Khi tag `ThoiGianCapLieu` nhảy lên `> 0` ➔ Hệ thống tự động chuyển trạng thái mẻ trộn thành `Active` và lưu `start_time = DateTime.Now`.
3. **Bước 3**: Khi có cảnh báo lỗi xảy ra trên tag (ví dụ: `AFChemTX01.ThoiGianCapLieu` nhảy lên `16`) ➔ `AlarmLogger` nhận sự kiện, tách tên thiết bị (mặc định là `TX01` nếu tag không chứa tiền tố thiết bị hợp lệ) và thực hiện truy vấn `GetActiveBatchId`. Nếu chưa có mẻ `Active` nào, nó tự động kích hoạt mẻ `Pending` cũ nhất (FIFO) hoặc tự tạo mẻ khẩn cấp `Active` mới. Tiếp theo ghi nhận lỗi vào bảng `alarmlog` kèm theo `batchId` hợp lệ đã được truy vết/tạo lập.
4. **Bước 4**: Định kỳ 30 giây, dữ liệu báo cáo mẻ trộn được ghi vào `alarmreport` kèm theo `batchId` tương ứng.
5. **Bước 5**: Khi các tag công đoạn chuyển tiếp đến bước 5, và cả hai tag `ThoiGianXaHang` cùng `ThoiGianRungXaHang` đều quay trở về `0` ➔ Hệ thống tự động chuyển trạng thái mẻ trộn thành `Completed` và lưu `end_time = DateTime.Now`.
6. **Bước 6 (Khôi phục cảnh báo)**: Khi tag trở về `0`, hệ thống thực hiện `ON DUPLICATE KEY UPDATE` cập nhật trạng thái dòng alarm thành `Resolved` (hoặc `Restored`) và giữ nguyên giá trị `batchId` ban đầu để tránh sai lệch dữ liệu sự kiện.

---

## 5. Các cải tiến kỹ thuật chống race condition và trùng lặp mẻ (Cập nhật 21/05/2026)

Để tránh hiện tượng cột `batchId` bị `NULL` và khắc phục triệt để lỗi race condition tạo thêm mẻ `Active` trùng lặp khi hai luồng khởi động cùng lúc, chúng tôi đã nâng cấp logic xử lý ở cả hai đầu như sau:

### 5.1. Nâng cấp trong `AlarmLogger.cs` (Luồng Cảnh báo Realtime)
1. **Trích xuất Tên Thiết Bị Mềm dẻo**: `ExtractDeviceName` được cải tiến để nếu không thể nhận diện định dạng `AFChem[DeviceName].[TagName]` thì sẽ mặc định trả về `"TX01"` thay vì trả về prefix thô như trước, phù hợp với thực tế vận hành 1 máy.
2. **Kích hoạt mẻ Pending (FIFO)**: Khi tìm kiếm `GetActiveBatchId`, nếu không có mẻ `Active` nào, logger sẽ tìm mẻ `Pending` cũ nhất (FIFO) của thiết bị đó trong DB, tự động chuyển nó sang `Active` (với `start_time` hiện tại) và sử dụng ID này.
3. **Mẻ khẩn cấp (Emergency Fallback)**: Nếu hoàn toàn không có mẻ nào (cả `Active` và `Pending`) trong cơ sở dữ liệu, logger sẽ tự động tạo ra một mẻ khẩn cấp với trạng thái `Active`, đặt tên tự động theo cấu trúc chuẩn `<DeviceName>-<yyyyMMdd>-<stt>` và trả về ID của mẻ đó để tránh `batchId` bị `NULL` trong mọi trường hợp.

### 5.2. Nâng cấp trong `AlarmReportLogger.cs` (Luồng Báo cáo 30s)
1. **Khóa Liên Kết Active Có Sẵn**: Tại hàm `LinkOrCreateActiveBatch`, trước khi tìm mẻ `Pending` hay cố gắng tạo mẻ khẩn cấp mới, hệ thống sẽ thực hiện truy vấn kiểm tra xem trong cơ sở dữ liệu **đã có mẻ nào đang ở trạng thái `Active` cho thiết bị này chưa**.
2. **Loại bỏ Trùng lặp**: Nếu đã tồn tại mẻ `Active` (được luồng alarm kích hoạt trước đó hoặc bởi tiến trình song song khác), luồng 30s sẽ trực tiếp liên kết đến ID mẻ hoạt động đó (`activeBatchId = ID_Active`), giữ nguyên `start_time` đồng bộ và kết thúc hàm ngay lập tức mà không tạo thêm bất kỳ bản ghi nào mới.


