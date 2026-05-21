# Kế hoạch triển khai (Implementation Tasks) - Module Quản lý Mẻ (Batches) và API

Dưới đây là danh sách các công việc chi tiết (Task List) để bổ sung tính năng quản lý mẻ trộn (`batches`), thiết lập Self-hosted HTTP API cho bên thứ 3 và tích hợp liên kết mẻ trộn với các báo cáo và cảnh báo.

---

## Giai đoạn 1: Thiết kế Cơ sở dữ liệu và Auto-Migration [ETA: 4h]
- [x] **Task 1.1:** Viết câu lệnh SQL khởi tạo bảng `batches` trong file migration hoặc trong hàm khởi tạo cơ sở dữ liệu.
- [x] **Task 1.2:** Bổ sung phương thức `AddBatchIdColumnIfNeeded` trong class `DataAccess` hoặc các logger (`AlarmReportLogger`, `AlarmLogger`) để tự động kiểm tra và thêm cột `batchId` vào bảng `alarmreport` và `alarmlog` nếu cột chưa tồn tại (Auto-Migration).

## Giai đoạn 2: Phát triển Self-hosted HTTP API Server [ETA: 6h]
- [x] **Task 2.1:** Tạo class `BatchesHttpServer` độc lập nằm trong dự án `HinoTools.Data` (thư mục `Http`).
- [x] **Task 2.2:** Triển khai cơ chế lắng nghe trên cổng `5500` sử dụng lớp `HttpListener` của .NET.
- [x] **Task 2.3:** Lập trình logic sinh tên mẻ (`device_name-date-stt`) tránh trùng lặp:
  - Truy vấn database để đếm số lượng mẻ đã có trong ngày cho thiết bị tương ứng nhằm lấy số thứ tự `stt` tiếp theo.
- [x] **Task 2.4:** Viết logic chèn bản ghi mẻ ở trạng thái `Pending` vào bảng `batches` và trả về kết quả định dạng JSON.

## Giai đoạn 3: Tích hợp Dynamic DeviceName & State Machine trong AlarmReportLogger [ETA: 6h]
- [x] **Task 3.1:** Viết helper `DeviceNameHelper` để tự động phân tách Device Name động (loại bỏ tiền tố `AFChem` và lấy tên thiết bị thực tế, ví dụ `AFChemTX01` -> `TX01`).
- [x] **Task 3.2:** Cập nhật class `AlarmReportLogger` để khi bắt đầu mẻ (`ThoiGianCapLieu > 0` và ở trạng thái Idle):
  - Sử dụng tên thiết bị được tách động để truy vấn DB lấy mẻ `Pending` cũ nhất (FIFO).
  - Cập nhật trạng thái mẻ thành `Active` và lưu ID mẻ vào biến bộ nhớ `activeBatchId`.
  - Cập nhật thời điểm `start_time = DateTime.Now`.
  - Nếu không có mẻ `Pending`, tự động sinh mẻ khẩn cấp/fallback ở trạng thái `Active`.
- [x] **Task 3.3:** Cập nhật logic chèn dữ liệu của `AlarmReportLogger` để điền `batchId` tương ứng vào cột `batchId` của bảng `alarmreport`.
- [x] **Task 3.4:** Cập nhật State Machine để khi mẻ trộn kết thúc (sau khi hoàn thành Công đoạn 5: `ThoiGianXaHang == 0` và `ThoiGianRungXaHang == 0`):
  - Cập nhật mẻ đang chạy thành `status = 'Completed'` và gán `end_time = DateTime.Now` vào database.
  - Reset `activeBatchId = null` trong bộ nhớ.

## Giai đoạn 4: Tích hợp liên kết cảnh báo sự cố (AlarmLogger & AlarmServer) [ETA: 4h]
- [x] **Task 4.1:** Cập nhật logic chèn cảnh báo của `AlarmLogger` (hoặc `AlarmServer` khi insert vào bảng `alarmlog`):
  - Tách tên thiết bị động từ `TagName` của cảnh báo.
  - Truy vấn bảng `batches` để lấy ID mẻ đang ở trạng thái `Active` ứng với thiết bị đó.
  - Điền ID mẻ vừa tìm được vào cột `batchId` của bản ghi cảnh báo.

## Giai đoạn 5: Tự động hóa khởi chạy HTTP Server và Kiểm thử [ETA: 6h]
- [x] **Task 5.1:** Cập nhật `TryInitialize` của `AlarmReportLogger` để tự động khởi tạo và chạy `BatchesHttpServer` trên cổng `5500` (được cấu hình qua thuộc tính). Tự động tắt server khi giải phóng component.
- [x] **Task 5.2:** Xây dựng tài liệu kiểm thử chi tiết và viết các kịch bản kiểm thử API (sử dụng curl hoặc PowerShell Invoke-RestMethod).
- [x] **Task 5.3:** Tiến hành chạy giả lập và xác minh tính đúng đắn của toàn bộ luồng dữ liệu (mẻ trộn API -> chuyển động trạng thái mẻ -> ghi log alarmreport -> ghi log alarmlog -> kết thúc mẻ).
