# Walkthrough - Cấu hình Thứ tự Chạy Mẻ sản xuất (Run Execution Ordering)

Tài liệu này mô tả chi tiết các chỉnh sửa đã thực hiện và kết quả kiểm thử cho tính năng sắp xếp thứ tự thực thi mẻ sản xuất (runs) động trên hệ thống core HinoTools.Alarm.

## 1. Mục tiêu và Giải pháp
Để giải quyết bài toán chạy xen kẽ các lô hàng khác nhau của khách hàng (ví dụ: đang chạy 1/2 lô AF101 thì tạm dừng để chạy lô AF102, sau đó mới quay lại chạy tiếp mẻ còn lại của AF101), chúng tôi đã:
- Thay thế cơ chế chọn mẻ kế tiếp dựa trên FIFO truyền thống (sắp xếp theo ID/thời gian tạo) bằng cơ chế sắp xếp động theo trường `execution_order` trong cơ sở dữ liệu.
- Cột `execution_order` được lưu trữ trong bảng `runs`, cho phép Web UI cập nhật thứ tự chạy ưu tiên tùy ý của các mẻ đang chờ (`Pending`). Core Server chịu trách nhiệm đọc và thực thi đúng thứ tự đã cấu hình.

---

## 2. Các thay đổi đã thực hiện

### 2.1 Database Migration
- Bổ sung cơ chế tự động migration trong [AlarmReportLogger.cs](file:///c:/Users/tanhv/Project/HinoTools.Alarm_27092023_Test/HinoTools.Alarm_27092023_Test/HinoTools.Data/Log/AlarmReportLogger.cs) và [BatchesHttpServer.cs](file:///c:/Users/tanhv/Project/HinoTools.Alarm_27092023_Test/HinoTools.Alarm_27092023_Test/HinoTools.Data/Http/BatchesHttpServer.cs):
  - Kiểm tra sự tồn tại của cột `execution_order` trong bảng `runs`.
  - Nếu chưa có, tự động thực thi `ALTER TABLE runs ADD COLUMN execution_order INT NOT NULL DEFAULT 0 AFTER status`.
  - Thực hiện di chuyển dữ liệu lịch sử một lần (one-time backfill): Gán `execution_order = id` cho các mẻ cũ để đảm bảo tính liên tục của hệ thống.

### 2.2 Core Monitoring & Selection Logic
- Cập nhật logic tìm kiếm mẻ chờ chạy tiếp theo trong `LinkOrCreateActiveBatch` thuộc [AlarmReportLogger.cs](file:///c:/Users/tanhv/Project/HinoTools.Alarm_27092023_Test/HinoTools.Alarm_27092023_Test/HinoTools.Data/Log/AlarmReportLogger.cs):
  - Thay đổi câu lệnh SQL sắp xếp ưu tiên mẻ có `execution_order` nhỏ nhất: `ORDER BY r.execution_order ASC, r.id ASC LIMIT 1`.
  - Đảm bảo gán `execution_order = 0` (mức ưu tiên tối cao) cho các mẻ khẩn cấp/tự động sinh do SCADA phát hiện chạy trực tiếp.

### 2.3 HTTP API & Webhook Creation Logic
- **Tạo Lô trực tiếp qua HTTP API ([BatchesHttpServer.cs](file:///c:/Users/tanhv/Project/HinoTools.Alarm_27092023_Test/HinoTools.Alarm_27092023_Test/HinoTools.Data/Http/BatchesHttpServer.cs)):**
  - Cập nhật API `POST /api/batches/create` để tính toán và tự động chèn `execution_order` tăng dần liên tiếp theo từng thiết bị (device-specific).
  - Sử dụng truy vấn `MAX(execution_order)` của các mẻ hiện hành thuộc thiết bị trước khi thêm mẻ mới.
  - Sửa đổi API `GET /api/runs` để trả về trường `execution_order` trong phản hồi JSON.
- **Tạo Lô bất đồng bộ qua Webhook ([WebhookHttpServer.cs](file:///c:/Users/tanhv/Project/HinoTools.Alarm_27092023_Test/HinoTools.Alarm_27092023_Test/HinoTools.Data/Http/WebhookHttpServer.cs)):**
  - Sửa đổi tiến trình xử lý BOM bất đồng bộ `ProcessWebhookAsync`.
  - Thực hiện truy vấn `MAX(execution_order)` cho thiết bị tương ứng trước khi chèn các mẻ con mới với giá trị thứ tự tăng dần liên tiếp.

### 2.4 Compensating Run Inheritance
- Cập nhật logic `FailActiveBatch()` trong [AlarmReportLogger.cs](file:///c:/Users/tanhv/Project/HinoTools.Alarm_27092023_Test/HinoTools.Alarm_27092023_Test/HinoTools.Data/Log/AlarmReportLogger.cs):
  - Khi mẻ đang chạy gặp lỗi và dừng, mẻ chạy bù (compensating run) được sinh ra sẽ tự động thừa hưởng giá trị `execution_order` của mẻ lỗi đó, đảm bảo mẻ bù này lập tức được đưa vào vị trí chờ ưu tiên cao nhất kế tiếp.

---

## 3. Kết quả Kiểm thử & Xác minh

Chúng tôi đã xây dựng và chạy kịch bản kiểm thử tích hợp tự động bằng PowerShell tại [test_execution_ordering.ps1](file:///c:/Users/tanhv/Project/HinoTools.Alarm_27092023_Test/HinoTools.Alarm_27092023_Test/scratch/test_execution_ordering.ps1). Kết quả kiểm thử thực tế như sau:

1. **MySQL Connection & Clean Up:** Kết nối MySQL thành công.
2. **Database Migration Verification:** Cột `execution_order` được tự động tạo và có kiểu dữ liệu là `int(11)`.
3. **HTTP API Batch Creation:**
   - Tạo lô mới thành công với 2 mẻ con.
   - Database lưu trữ chính xác `execution_order` lần lượt là `1` và `2`.
4. **Webhook Async Processing:**
   - Webhook tạo lô mới thành công.
   - Các mẻ được tạo bất đồng bộ tự động lấy thứ tự tiếp tục tăng dần liên tục: `3` và `4`.
5. **GET API Return Values:** API `GET /api/runs?batch_id=xxx` phản hồi đúng cấu trúc JSON chứa trường `"execution_order"`.
6. **Prioritization & Web UI Swap Simulation:**
   - Ban đầu mẻ ưu tiên nhất là `Me01` (order 1).
   - Mô phỏng Web UI swap thứ tự của mẻ 1 và mẻ 3 (`Me01` đổi thành order 3, `Me03` đổi thành order 1).
   - Query chọn mẻ chạy tiếp theo đã trả về chính xác mẻ `Me03` (được ưu tiên lên trước).
7. **Compensating Run Priority Inheritance:**
   - Khi mẻ đang chạy (order 1) bị đánh dấu trạng thái `Error`, mẻ bù mới được tạo ra thừa hưởng chính xác `execution_order = 1`.
   - Query chọn mẻ chạy tiếp theo đã ưu tiên mẻ chạy bù này lên đầu hàng đợi thành công.

Tất cả các ca kiểm thử tích hợp đều vượt qua 100% thành công mà không có bất kỳ lỗi nào.
