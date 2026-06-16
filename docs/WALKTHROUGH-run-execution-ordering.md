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

### 2.5 Khắc phục lỗi Xung đột Kích hoạt giữa các Project (Split-Brain Fix)
- **Vấn đề phát sinh (Race Condition):** Khi mẻ 1 kết thúc thành công (`Completed`), mẻ 2 chuyển sang trạng thái chờ chạy. Khi mẻ 2 bắt đầu, cả `AlarmReportLogger` (chạy ngầm chu kỳ 30s) và `AlarmLogger` (nhận sự kiện thời gian thực từ WCF) đều cố gắng xử lý kích hoạt mẻ này.
  - Nếu `AlarmReportLogger` quét trước và cập nhật mẻ 2 thành `Active`.
  - Tiếp theo `AlarmLogger` nhận được sự kiện `isNewRunStart = true` (khi tag `ThoiGianCapLieu` chuyển sang `Alarm`). Vì thấy trong cơ sở dữ liệu đã có một mẻ đang ở trạng thái `Active` (là mẻ 2 vừa mới kích hoạt), `AlarmLogger` cho rằng đây là mẻ cũ bị lỗi chưa đóng và bị hủy ngang bởi mẻ mới này.
  - Do đó, `AlarmLogger` cập nhật nhầm mẻ 2 thành `Error` và tự động tạo mẻ 3 `Pending` để bù mẻ, mặc dù mẻ 2 thực tế chưa hề được chạy.
- **Giải pháp xử lý (Fix):**
  - Chỉnh sửa `GetActiveBatchAndRunId()` trong [AlarmLogger.cs](file:///c:/Users/tanhv/Project/HinoTools.Alarm_27092023_Test/HinoTools.Alarm_27092023_Test/HinoTools.Alarm/Control/AlarmLogger.cs) để đọc thêm trường `start_time` của mẻ đang `Active`.
  - Nếu thời gian bắt đầu của mẻ `Active` đó nằm trong vòng 60 giây gần nhất (`(DateTime.Now - startTime).TotalSeconds < 60`), hệ thống xác định đây là mẻ sản xuất **vừa mới được kích hoạt** cho chính chu kỳ hiện tại bởi logger khác, chứ không phải mẻ cũ bị dở dang từ trước.
  - Do đó, `AlarmLogger` sẽ trực tiếp tái sử dụng (`re-use`) mẻ này mà không đánh dấu nó thành `Error` hay tạo mẻ bù.
  - Đồng bộ hóa logic tìm kiếm mẻ `Pending` tiếp theo và tạo mẻ emergency trong `AlarmLogger.cs` để sử dụng và kế thừa trường `execution_order` thống nhất với core logic.
  - Kịch bản kiểm thử độc lập đã được bổ sung tại [test_split_brain_issue.ps1](file:///c:/Users/tanhv/Project/HinoTools.Alarm_27092023_Test/HinoTools.Alarm_27092023_Test/scratch/test_split_brain_issue.ps1) để xác minh và vượt qua hoàn toàn 100% (mẻ 2 được giữ nguyên trạng thái `Active`, không bị lỗi hóa, và không bị tạo mẻ 3 thừa).

---

## 4. Công cụ Khôi phục Dữ liệu từ Webhook Log (Data Recovery Tool)

Chúng tôi đã thiết kế và triển khai script PowerShell [recreate_from_webhook_log.ps1](file:///c:/Users/tanhv/Project/HinoTools.Alarm_27092023_Test/HinoTools.Alarm_27092023_Test/scratch/recreate_from_webhook_log.ps1) giúp tái thiết lập lại toàn bộ dữ liệu bảng `batches`, `runs`, và `run_info` từ dữ liệu lịch sử thô có sẵn trong bảng `webhook_logs`.

### 4.1 Cơ chế và tính năng nâng cấp:
1. **Dọn dẹp và Tái thiết lập (`-Reset`):**
   - Hỗ trợ tham số `[switch]$Reset`. Khi chạy với `-Reset`, script sẽ tạm thời vô hiệu hóa kiểm tra khóa ngoại (`FOREIGN_KEY_CHECKS = 0`), dọn sạch (`TRUNCATE`) 3 bảng mục tiêu (`run_info`, `runs`, `batches`), sau đó bật lại kiểm tra khóa ngoại.
2. **Xử lý tuần tự và Lọc bản ghi:**
   - Script chỉ truy vấn các bản ghi `webhook_logs` có trạng thái `'Completed'` (hoặc ID log cụ thể nếu truyền tham số `$LogId`) để đảm bảo chỉ những mẻ từng chạy thành công mới được dựng lại. Các log này được xử lý tuần tự theo thời gian tăng dần (`id ASC`).
3. **Quản lý Số Thứ Tự (STT) và Execution Order Trong Bộ Nhớ:**
   - Để tránh các mẻ bị trùng lặp số thứ tự hoặc nhảy số sai lệch khi truy vấn DB liên tiếp trên môi trường trống vừa reset, script duy trì cấu trúc theo dõi trong bộ nhớ:
     - `$batchSequenceTracker`: Đếm và gán STT (ví dụ `-01`, `-02`) tăng dần từ `01` cho mỗi thiết bị trong mỗi ngày.
     - `$executionOrderTracker`: Lưu trữ và kế thừa liên tục giá trị `execution_order` tăng dần cho các mẻ con thuộc từng thiết bị.
4. **Trích xuất BOM tự động:**
   - Tự động giải mã Base64 dữ liệu BOM của từng mẻ con (`custom_thong_tin_bom_san_xuat_a`, `_b`, ...) và nạp đầy đủ thông tin nguyên vật liệu vào bảng `run_info`.

### 4.2 Kết quả Kiểm thử Xác minh:
- Đã chạy thử nghiệm thực tế trên cơ sở dữ liệu local với tham số `-Reset`.
- Script đã dọn sạch các bảng thành công, tìm thấy các webhook log có trạng thái `'Completed'`, và tái tạo lại chính xác:
  - Mẻ 1: `TX01-20260616-01` với 2 mẻ con có `execution_order` là `1` và `2`.
  - Mẻ 2: `TX01-20260616-02` với 2 mẻ con có `execution_order` là `3` và `4`.
  - Nạp đầy đủ thông tin BOM vào `run_info` thành công.

