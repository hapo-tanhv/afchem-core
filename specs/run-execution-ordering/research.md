# Research & Design Decisions

---
**Purpose**: Capture discovery findings, architectural investigations, and rationale that inform the technical design for the run execution ordering feature.
---

## Summary
- **Feature**: `run-execution-ordering`
- **Discovery Scope**: Extension (Existing System)
- **Key Findings**:
  - Bảng `runs` hiện tại được khởi tạo thông qua logic migration tự động tại `AlarmReportLogger.EnsureBatchesTableExists` và `BatchesHttpServer.EnsureBatchesTableExists`. Cần bổ sung cột `execution_order` dạng `INT NOT NULL DEFAULT 0` vào cả hai hàm migration này.
  - Cần thực hiện migration một lần (one-time migration) cho dữ liệu lịch sử để gán `execution_order = id`, giúp các mẻ cũ giữ nguyên thứ tự chạy tuyến tính mà không bị xung đột.
  - Logic xác định mẻ tiếp theo trong `AlarmReportLogger.LinkOrCreateActiveBatch` sẽ được thay đổi từ sắp xếp theo `b.id ASC, r.run_number ASC` sang `r.execution_order ASC, r.id ASC`.
  - Để hỗ trợ gán thứ tự chạy liên tục tăng dần theo thiết bị, hàm tạo batch trong `BatchesHttpServer.cs` và `WebhookHttpServer.cs` cần truy vấn `MAX(execution_order)` của các mẻ thuộc thiết bị đó trước khi thực hiện chèn mẻ mới.

## Research Log

### 1. Database Schema Migration for C# and MySQL
- **Context**: Bằng cách nào hệ thống tự động cập nhật cấu trúc database mà không cần chạy script SQL thủ công?
- **Sources Consulted**: `HinoTools.Data/Log/AlarmReportLogger.cs`, `HinoTools.Data/Http/BatchesHttpServer.cs`
- **Findings**:
  - Hệ thống sử dụng thư viện `MySql.Data.MySqlClient` và tự thực thi các lệnh `ALTER TABLE` khi chạy hàm `EnsureBatchesTableExists`.
  - Cột `execution_order` mới sẽ được tự động thêm vào thông qua kiểm tra: `SHOW COLUMNS FROM \`runs\` LIKE 'execution_order'`.
- **Implications**: Cần sửa đổi logic khởi động ở cả hai component (Core Logger và HTTP Server) để đồng bộ hóa cấu trúc DB.

### 2. Run Sequence Query Rationale (Per Device or Global?)
- **Context**: Thứ tự chạy của các mẻ (execution_order) nên được tính độc lập theo từng thiết bị (device) hay chung toàn hệ thống?
- **Sources Consulted**: Trao đổi làm rõ với khách hàng và phân tích logic FIFO hiện tại trong `AlarmReportLogger.cs`.
- **Findings**:
  - Mỗi thiết bị (`TX01`, `TX02`...) có luồng vận hành PLC độc lập và giám sát riêng biệt.
  - Logic FIFO hiện tại lọc theo `device_name`: `WHERE b.device_name = '{deviceName}'`.
- **Implications**: Giá trị `MAX(execution_order)` khi tạo mẻ mới phải được lọc theo `device_name` tương ứng để đảm bảo thứ tự chạy liên tục và chính xác cho từng máy.

## Architecture Pattern Evaluation

| Option | Description | Strengths | Risks / Limitations | Notes |
|--------|-------------|-----------|---------------------|-------|
| **DB-Driven Priority Queue** | Thay đổi câu lệnh SQL truy vấn mẻ active tiếp theo bằng cách sắp xếp theo trường `execution_order` mới. | Cực kỳ đơn giản, không thay đổi luồng nghiệp vụ hiện tại, tương thích ngược tốt. | Cần đảm bảo cột `execution_order` luôn được đánh chỉ mục để truy vấn nhanh. | Tiếp cận tối ưu nhất cho hệ thống Core hiện tại. |
| **In-Memory Priority Queue** | Core lưu trữ hàng đợi các mẻ chạy trong bộ nhớ và đồng bộ với DB. | Tốc độ truy xuất nhanh. | Dễ mất đồng bộ khi Core restart hoặc khi Web UI thay đổi dữ liệu trực tiếp trong DB. | Không khuyến khích do rủi ro bất đồng bộ cao. |

## Design Decisions

### Decision 1: Thêm cột `execution_order` vào bảng `runs` và tự động Migration
- **Context**: Cần lưu trữ thứ tự chạy của từng mẻ và đồng bộ dữ liệu cũ.
- **Alternatives Considered**:
  1. Thêm cột nullable `execution_order INT NULL`.
  2. Thêm cột `execution_order INT NOT NULL DEFAULT 0` (Được chọn).
- **Rationale**: Sử dụng `NOT NULL DEFAULT 0` giúp tránh lỗi `NullReferenceException` hoặc ép kiểu phức tạp trong C#.
- **Trade-offs**: Bản ghi cũ sẽ nhận giá trị mặc định là `0`. Vì thế, cần chạy câu lệnh SQL cập nhật dữ liệu lịch sử một lần duy nhất: `UPDATE \`runs\` SET \`execution_order\` = \`id\` WHERE \`execution_order\` = 0`.

### Decision 2: Truy vấn `MAX(execution_order)` theo Device Name khi tạo Lô/Mẻ mới
- **Context**: Đảm bảo các mẻ của lô hàng mới tiếp theo có thứ tự chạy tiếp nối mẻ cũ của cùng thiết bị.
- **Selected Approach**: Thực hiện câu lệnh `SELECT IFNULL(MAX(r.execution_order), 0) FROM \`runs\` r JOIN \`batches\` b ON r.batch_id = b.id WHERE b.device_name = @device_name` trước khi chèn các mẻ con mới.
- **Rationale**: Đảm bảo tính nhất quán của chuỗi số thứ tự cho từng thiết bị mà không bị ảnh hưởng bởi tiến trình của thiết bị khác.

## Risks & Mitigations
- **Bất đồng bộ thứ tự khi nhiều yêu cầu tạo batch xảy ra đồng thời (Race Condition)**:
  - *Mitigation*: Cả hai server API (`BatchesHttpServer.cs` và `WebhookHttpServer.cs`) đều đã sử dụng cơ chế khóa dùng chung `lock (dbLock)` khi thực hiện các hoạt động ghi DB. Việc truy vấn Max và Insert mẻ mới sẽ nằm trọn vẹn trong block lock này để đảm bảo tính tuần tự.
- **Trùng lặp số thứ tự chạy sau khi Web UI cập nhật**:
  - *Mitigation*: Logic truy vấn mẻ tiếp theo của Core sử dụng tiêu chí sắp xếp phụ: `ORDER BY r.execution_order ASC, r.id ASC`. Nếu Web UI vô tình cập nhật hai mẻ có cùng số thứ tự chạy, mẻ nào được tạo trước (ID nhỏ hơn) sẽ được ưu tiên chạy trước, tránh treo hệ thống.
