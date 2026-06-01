# Kế hoạch triển khai (Implementation Tasks) - Module Quản lý Batch & Run Nâng cấp

Dưới đây là danh sách các công việc chi tiết (Task List) để thực hiện việc nâng cấp hệ thống đáp ứng bài toán: **1 Batch (Lô) gồm nhiều Mẻ sản xuất (Run)**, mỗi mẻ luôn gồm **8 công đoạn** hoạt động liên tục.

---

## Giai đoạn 1: Nâng cấp Cơ sở dữ liệu và Auto-Migration [ETA: 4h]
- [x] **Task 1.1:** Thiết kế câu lệnh SQL tạo bảng mới `runs` (Mẻ sản xuất) liên kết khóa ngoại với bảng `batches`.
- [x] **Task 1.2:** Cập nhật phương thức `EnsureBatchesTableExists()` trong `AlarmReportLogger.cs` và `BatchesHttpServer.cs` để:
  - Tạo bảng `runs` nếu chưa tồn tại.
  - Tự động bổ sung cột `total_runs` kiểu `INT NOT NULL DEFAULT 1` vào bảng `batches`.
- [x] **Task 1.3:** Cập nhật phương thức `AddBatchIdColumnIfNeeded` (hoặc tạo phương thức mới `AddRunIdColumnIfNeeded`) tại `AlarmReportLogger.cs`, `AlarmLogger.cs`, và `RealtimeThresholdLogger.cs` để tự động kiểm tra và thêm cột `runId` (`INT NULL DEFAULT NULL`) vào các bảng `alarmreport`, `alarmlog`, và `realtime_alarms`.
- [x] **Task 1.4:** Viết câu lệnh SQL di chuyển dữ liệu (Historical Migration) chạy tự động 1 lần duy nhất:
  - Tự động sinh 1 mẻ con trong bảng `runs` cho mỗi dòng dữ liệu `batches` lịch sử.
  - Ánh xạ lại chính xác cột `runId` dựa trên `batchId` cho các log báo cáo và nhật ký sự cố cũ.

## Giai đoạn 2: Nâng cấp Self-hosted HTTP API Server [ETA: 6h]
- [x] **Task 2.1:** Cập nhật API `POST /api/batches/create` trong `BatchesHttpServer.cs`:
  - Cho phép nhận tham số `runs_count` từ JSON body (mặc định bằng `1`).
  - Thêm cột `total_runs` vào câu lệnh INSERT vào bảng `batches`.
  - Thực thi vòng lặp để chèn `runs_count` mẻ con vào bảng `runs` ở trạng thái `Pending`.
  - Cập nhật định dạng JSON trả về chứa đầy đủ danh sách mẻ con vừa được tạo.
- [x] **Task 2.2:** Xây dựng API mới `GET /api/batches` trong `BatchesHttpServer.cs`:
  - Hỗ trợ tham số truy vấn lọc theo `device_name` và giới hạn kết quả trả về `limit` (mặc định 50).
  - Trả về danh sách Lô sắp xếp theo thời gian mới nhất.
- [x] **Task 2.3:** Xây dựng API mới `GET /api/runs` trong `BatchesHttpServer.cs`:
  - Bắt buộc tham số `batch_id`.
  - Trả về danh sách tất cả các Mẻ thuộc Batch đó để giao diện người dùng hiển thị dropdown mẻ.

## Giai đoạn 3: Cập nhật State Machine trong AlarmReportLogger [ETA: 6h]
- [x] **Task 3.1:** Thêm biến thành viên `activeRunId` dạng `int?` vào `AlarmReportLogger.cs` để lưu trữ ID mẻ con hiện tại.
- [x] **Task 3.2:** Cập nhật phương thức `LinkOrCreateActiveBatch()` trong `AlarmReportLogger.cs`:
  - **Quét khôi phục**: Kiểm tra xem có mẻ con nào của thiết bị đang ở trạng thái `Active` trong cơ sở dữ liệu để gán lại `activeRunId` và `activeBatchId` khi khởi động ứng dụng.
  - **Quét FIFO**: Truy vấn lấy mẻ con có trạng thái `Pending` cũ nhất thuộc các lô sản xuất `Pending` hoặc `Active`.
  - **Kích hoạt mẻ**: Cập nhật trạng thái mẻ con thành `Active` và Batch cha thành `Active` (nếu lô cha đang là `Pending`).
  - **Fallback**: Nếu không có mẻ `Pending` nào, tự động tạo mới Lô khẩn cấp và Mẻ khẩn cấp và thiết lập trạng thái `Active` cho cả hai.
- [x] **Task 3.3:** Cập nhật phương thức `InsertAlarmReport()` và các câu lệnh ghi log realtime:
  - Ghi nhận đồng thời `batchId` = `activeBatchId` và `runId` = `activeRunId` khi chèn dữ liệu vào bảng `alarmreport`.
- [x] **Task 3.4:** Cập nhật phương thức `CompleteActiveBatch()` khi kết thúc công đoạn 8:
  - Cập nhật trạng thái mẻ con (`runs`) hiện tại thành `Completed` kèm theo thời gian hoàn thành.
  - Thực thi kiểm tra đếm số mẻ chưa hoàn thành trong Lô cha.
  - Nếu tất cả đã hoàn thành, cập nhật trạng thái Lô (`batches`) thành `Completed` và gán thời gian kết thúc. Nếu còn mẻ chưa hoàn thành, giữ nguyên trạng thái Lô là `Active`.
  - Giải phóng bộ nhớ `activeRunId` và `activeBatchId` về `null`.

## Giai đoạn 4: Liên kết Cảnh báo sự cố (AlarmLogger & AlarmServer) [ETA: 4h]
- [x] **Task 4.1:** Cập nhật logic `InsertAlarm()` trong `AlarmLogger.cs`:
  - Tách tên thiết bị động từ Tag sự cố.
  - Truy vấn database tìm Mẻ sản xuất (`runs`) đang ở trạng thái `Active` của thiết bị đó.
  - Gán cả `batchId` và `runId` tìm được vào câu lệnh INSERT cảnh báo sự cố vào bảng `alarmlog`.

## Giai đoạn 5: Kiểm thử và Xác nhận (Testing & Verification) [ETA: 4h]
- [x] **Task 5.1:** Thực hiện kiểm thử tích hợp (Integration Tests) gọi các API tạo Batch nhiều mẻ.
- [x] **Task 5.2:** Giả lập chạy SCADA qua 8 công đoạn, xác nhận:
  - Trạng thái Lô và Mẻ con chuyển đổi chính xác theo FIFO.
  - Khi hoàn thành mẻ 1 của lô có 2 mẻ, lô cha vẫn giữ nguyên trạng thái `Active`.
  - Khi hoàn thành mẻ 2, lô cha chuyển sang trạng thái `Completed`.
- [x] **Task 5.3:** Xác minh tính chính xác của dữ liệu lịch sử sau khi migrate dữ liệu.
