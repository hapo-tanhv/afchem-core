# Yêu cầu Kỹ thuật: Hệ thống Quản lý Mẻ (Batches Management) và Tích hợp API

Tài liệu này xác định các yêu cầu kỹ thuật và nghiệp vụ cho việc bổ sung tính năng quản lý mẻ trộn (`batches`), tích hợp HTTP API tự lưu trữ cho bên thứ ba, và liên kết mẻ trộn với các báo cáo (`alarmreport`) và nhật ký cảnh báo (`alarmlog`).

---

## 1. Yêu cầu Cơ sở dữ liệu (Database Schema)

Hệ thống cần tự động khởi tạo và cập nhật cấu trúc cơ sở dữ liệu để quản lý mẻ.

### 1.1. Bảng mới `batches`
Bảng `batches` được thiết kế để quản lý vòng đời của từng mẻ trộn:
- **`id`**: `INT AUTO_INCREMENT PRIMARY KEY` - Khóa chính tự tăng.
- **`name`**: `VARCHAR(100) NOT NULL UNIQUE` - Tên mẻ, sinh tự động theo quy tắc.
- **`device_name`**: `VARCHAR(100) NOT NULL` - Tên thiết bị thực tế chạy mẻ (ví dụ: `TX01`).
- **`status`**: `VARCHAR(50) NOT NULL DEFAULT 'Pending'` - Trạng thái của mẻ. Các giá trị hợp lệ:
  - `Pending`: Mẻ đã được tạo bởi bên thứ ba qua API nhưng chưa bắt đầu chạy trên PLC/SCADA.
  - `Active`: Mẻ đang được chạy (phát hiện khi `ThoiGianCapLieu > 0` lần đầu).
  - `Completed`: Mẻ đã kết thúc (phát hiện khi cả `ThoiGianXaHang` và `ThoiGianRungXaHang` cùng về 0).
- **`start_time`**: `DATETIME NULL` - Thời gian bắt đầu thực tế khi bắt đầu mẻ.
- **`end_time`**: `DATETIME NULL` - Thời gian kết thúc thực tế khi hoàn thành mẻ.

### 1.2. Cập nhật bảng `alarmreport` và `alarmlog`
Bổ sung thêm cột liên kết mẻ trộn:
- **`batchId`**: `INT NULL` - Khóa ngoại (foreign key) liên kết tới bảng `batches(id)`.
- Hệ thống phải có cơ chế **Auto-Migration** (tự động nâng cấp bảng):
  - Khi khởi động, các logger (`AlarmReportLogger` và `AlarmLogger`) phải kiểm tra xem cột `batchId` đã tồn tại trong bảng tương ứng chưa.
  - Nếu chưa tồn tại, tự động thực thi câu lệnh SQL `ALTER TABLE ... ADD COLUMN batchId INT NULL` để thêm cột vào bảng.

---

## 2. Thiết kế HTTP API tự lưu trữ (Self-Hosted HTTP Web API)

Hệ thống cung cấp một HTTP API siêu nhẹ chạy ngầm sử dụng lớp `HttpListener` của .NET để bên thứ ba dễ dàng gọi qua request HTTP thông thường.

### 2.1. Cấu hình
- **Giao thức**: HTTP REST API.
- **Cổng mặc định**: `5500`.
- **Base URL**: `http://localhost:5500/`
- **Địa chỉ lắng nghe**: Tự động mở rộng lắng nghe từ mọi địa chỉ IP (`http://*:5500/` hoặc `http://localhost:5500/`).

### 2.2. Điểm cuối tạo mẻ (Create Batch Endpoint)
- **Method**: `POST`
- **Path**: `/api/batches/create`
- **Request Body (JSON - Tùy chọn)**:
  ```json
  {
    "device_name": "TX01"
  }
  ```
  - *Lưu ý*: Nếu body trống hoặc không truyền `device_name`, hệ thống sử dụng giá trị mặc định là một hằng số `TX01` (`const string DefaultDevice = "TX01"`).
- **Response (JSON)**:
  ```json
  {
    "success": true,
    "id": 1,
    "name": "TX01-20260520-01",
    "device_name": "TX01",
    "status": "Pending"
  }
  ```
  - *Mã lỗi HTTP*: Trả về `200 OK` nếu tạo thành công, `400 Bad Request` hoặc `500 Internal Server Error` kèm thông điệp lỗi dạng JSON nếu xảy ra lỗi.

### 2.3. Quy tắc sinh Tên Mẻ (Batch Name Generation)
Tên mẻ được sinh theo định dạng: `device_name-date-stt`
- **`device_name`**: Lấy từ request hoặc mặc định là `TX01`.
- **`date`**: Ngày hiện tại của hệ thống theo định dạng `yyyyMMdd` (ví dụ: `20260520`).
- **`stt`**: Số thứ tự (Sequence Number) gồm 2 chữ số tự tăng (ví dụ: `01`, `02`, `03`...), bắt đầu từ `01` cho mỗi ngày mới và tăng dần cho các mẻ tiếp theo được tạo trong cùng một ngày cho cùng một thiết bị (`device_name`).
- *Cơ chế truy vấn*: Hệ thống sẽ đếm số lượng mẻ hoặc tìm mẻ lớn nhất của thiết bị đó trong ngày hiện tại để sinh ra số thứ tự tiếp theo một cách an toàn và tránh trùng lặp.

---

## 3. Quy trình Tích hợp và Liên kết mẻ trộn (Workflow Integration)

### 3.1. Hàng đợi FIFO và Nhận dạng mẻ
Mỗi mẻ trộn (batch) tương ứng với **5 công đoạn hoạt động liên tục** (`CongDoanMay` đi từ 1 đến 5). Khi bên thứ ba tạo các mẻ qua API, hệ thống sẽ lưu chúng ở trạng thái `Pending` trong database.
1. **Bắt đầu mẻ (Công đoạn 1 - Cấp liệu)**: Khi máy bắt đầu chạy (`ThoiGianCapLieu > 0` và hệ thống đang ở trạng thái Idle `currentCongDoan == 0`):
   - **Tách Device Name động**: Lấy tiền tố trước dấu `.` của tag đầu tiên (ví dụ: `AFChemTX01.ThoiGianCapLieu` -> Device prefix là `AFChemTX01`). 
   - **Ánh xạ Tên Thiết Bị**: Chuyển đổi tên prefix từ định dạng ATSCADA sang tên thiết bị thực tế trong DB:
     - Nếu tên bắt đầu bằng `"AFChem"`, loại bỏ tiền tố này để lấy tên thiết bị thực tế (ví dụ: `AFChemTX01` -> `TX01`, `AFChemTX01` -> `PLC`).
     - Nếu không, giữ nguyên tên prefix làm tên thiết bị.
   - **Truy vấn FIFO**: Tìm bản ghi mẻ cũ nhất có `status = 'Pending'` ứng với thiết bị thực tế đó (ví dụ: `device_name = 'TX01'`).
   - **Kích hoạt mẻ**:
     - Nếu tìm thấy, cập nhật trạng thái mẻ đó thành `Active`, gán `start_time = DateTime.Now` và lưu `batchId` làm mẻ hiện hành đang chạy ngầm (`ActiveBatchId`).
     - Nếu **không tìm thấy** bất kỳ mẻ `Pending` nào trong DB:
       - Hệ thống tự động tạo một mẻ mới trên DB với trạng thái `Active`, tự động sinh tên mẻ theo quy tắc tại thời điểm đó để đảm bảo dữ liệu ghi log báo cáo không bị gián đoạn (Self-healing/Fallback mechanism).
2. **Trong suốt 5 công đoạn (Công đoạn 1 đến Công đoạn 5)**:
   - Mọi bản ghi được chèn vào bảng `alarmreport` sẽ được điền giá trị `batchId` bằng `ActiveBatchId`.

### 3.2. Hoàn thành mẻ trộn (Kết thúc Công đoạn 5)
Khi công đoạn 5 (Xả hàng) kết thúc hoàn toàn (thỏa mãn đồng thời cả hai giá trị `ThoiGianXaHang == 0` và `ThoiGianRungXaHang == 0` sau khi đã chạy trước đó, đồng nghĩa với việc kết thúc trọn vẹn 5 công đoạn của 1 mẻ):
- Hệ thống cập nhật bản ghi mẻ có ID là `ActiveBatchId` thành `status = 'Completed'`, gán `end_time = DateTime.Now`.
- Sau đó giải phóng trạng thái mẻ hiện hành (`ActiveBatchId = null`).
- Hệ thống quay về trạng thái `Idle` chờ mẻ tiếp theo.

### 3.3. Nhật ký cảnh báo (`alarmlog`)
Khi có cảnh báo sự cố phát sinh (`AlarmLogger` nhận sự kiện từ `AlarmServer`):
- Hệ thống trích xuất tên thiết bị từ `TagName` của cảnh báo phát sinh (ví dụ: `AFChemTX01.NhietDoMay` -> thiết bị là `TX01`).
- Thực hiện truy vấn cơ sở dữ liệu để tìm mẻ có `status = 'Active'` ứng với thiết bị `TX01` tại thời điểm đó.
- Nếu có mẻ `Active` tương ứng, lưu `batchId` của mẻ này vào dòng cảnh báo chèn trong bảng `alarmlog`. Nếu không, để trống (`NULL`).

---

## 4. Cơ chế Dynamic Device Name (Không hardcode `AFChemTX01`)

Hệ thống loại bỏ hoàn toàn việc hardcode chuỗi `"AFChemTX01"` bằng cơ chế tự động phân tách động:
- **Nguyên tắc**: Bất kỳ khi nào trích xuất tên thiết bị, hệ thống sử dụng phương thức tách chuỗi:
  ```csharp
  // Ví dụ: tag = "AFChemTX01.ThoiGianCapLieu" -> prefix = "AFChemTX01"
  string dotPrefix = tag.Contains(".") ? tag.Substring(0, tag.IndexOf('.')) : tag;
  // Loại bỏ "AFChem" nếu có để lấy tên thiết bị thực tế trong DB: "TX01"
  string dbDeviceName = dotPrefix.StartsWith("AFChem") ? dotPrefix.Substring(6) : dotPrefix;
  ```
- Cơ chế này áp dụng đồng nhất trên toàn bộ các component: `AlarmReportLogger`, `RealtimeThresholdLogger`, `AlarmLogger`, và `AlarmServer`.
