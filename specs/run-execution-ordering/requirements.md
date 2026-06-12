# Requirements Document

## Introduction
Dự án HinoTools.Alarm đã có cấu trúc quản lý Lô (Batch) và Mẻ (Run) với quan hệ 1-N được triển khai ở các thành phần `BatchesHttpServer.cs`, `WebhookHttpServer.cs` và giám sát bởi `AlarmReportLogger.cs`. Hiện tại, cơ chế chọn mẻ chạy tiếp theo dựa trên FIFO thuần túy (lấy mẻ Pending cũ nhất theo ID/thời gian tạo). Tài liệu này đặc tả yêu cầu nâng cấp thêm cột `execution_order` trong bảng `runs` để kiểm soát thứ tự chạy động từ Web UI, đồng thời cập nhật logic tìm mẻ trong `AlarmReportLogger` và cơ chế tạo mẻ trong các HTTP Server.

## Requirements

### Requirement 1: Database Schema Migration
**Objective:** As a System Database Migrator, I want to add an execution order column to the runs table, so that the system can store the execution sequence of each run.

#### Acceptance Criteria
1. The Database Migration Module shall tự động thêm cột `execution_order` (kiểu INT, cho phép NULL) vào bảng `runs` nếu cột này chưa tồn tại khi hệ thống khởi động.
2. The Database Migration Module shall cập nhật dữ liệu lịch sử bằng cách gán giá trị mặc định cho cột `execution_order` bằng giá trị `id` của mẻ đó đối với các bản ghi `runs` cũ chưa có thứ tự chạy.

### Requirement 2: Next Run Query Logic Enhancement
**Objective:** As a Core State Monitor, I want to retrieve the next run to execute based on the execution order sequence, so that the system runs batches in the operator-specified order.

#### Acceptance Criteria
1. When PLC bắt đầu chạy mẻ mới, the AlarmReportLogger shall truy vấn mẻ có trạng thái 'Pending' cho thiết bị tương ứng và sắp xếp tăng dần theo cột `execution_order` (trường hợp trùng giá trị `execution_order` sẽ ưu tiên mẻ có `id` nhỏ hơn) làm mẻ chạy hiện hành.
2. If không tìm thấy mẻ 'Pending' nào, then the AlarmReportLogger shall tự động sinh lô và mẻ khẩn cấp (fallback) ở trạng thái 'Active' và đặt giá trị `execution_order` mặc định để hệ thống ghi log không bị gián đoạn.

### Requirement 3: HTTP API Creation Sequence Assigning
**Objective:** As an API Consumer, I want to have sequence numbers assigned to newly created runs when batches are created, so that they have initial order values.

#### Acceptance Criteria
1. When nhận yêu cầu tạo lô hàng mới từ POST `/api/batches/create` hoặc webhook POST `/api/webhook`, the BatchesHttpServer/WebhookHttpServer shall truy vấn giá trị `execution_order` lớn nhất hiện tại của các mẻ 'Pending' trên cùng thiết bị để xác định giá trị bắt đầu.
2. The BatchesHttpServer/WebhookHttpServer shall tự động gán giá trị `execution_order` tăng dần liên tiếp cho các mẻ con mới được sinh ra trong lô đó (bắt đầu từ giá trị lớn nhất cộng thêm 1).
3. When nhận yêu cầu GET `/api/runs?batch_id=xxx`, the BatchesHttpServer shall trả về trường `execution_order` trong thông tin chi tiết của từng mẻ.
