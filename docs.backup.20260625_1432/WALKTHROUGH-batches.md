# Tài liệu tổng kết nâng cấp: Mô hình Lô (Batch) - Mẻ sản xuất (Run) (1-N) và Tích hợp HTTP API

Tài liệu này tổng hợp toàn bộ các thay đổi kiến trúc, cấu trúc cơ sở dữ liệu, các điểm cuối API nâng cấp, và hướng dẫn vận hành cho mô hình **1 Batch (Lô) chứa nhiều Mẻ sản xuất (Run)** hoạt động độc lập.

---

## 1. Kiến trúc nâng cấp Lô - Mẻ con (Batch - Run 1-N)

Theo yêu cầu mới từ khách hàng, hệ thống đã được tái cấu trúc hoàn toàn để tách biệt khái niệm **Lô sản xuất (Batch)** và **Mẻ sản xuất (Run)**:
- **Lô sản xuất (`batches`)**: Đại diện cho một lệnh sản xuất tổng thể (ví dụ: chạy Lô A gồm 2 mẻ con: mẻ 1 chạy sáng, mẻ 2 chạy chiều).
- **Mẻ sản xuất (`runs`)**: Đại diện cho một chu kỳ chạy thực tế gồm **8 công đoạn** của PLC (Cấp liệu ➔ Trộn 1 ➔ Xả đáy ➔ Rung xả đáy ➔ Hút xả đáy ➔ Trộn 2 ➔ Xả hàng ➔ Rung xả hàng).
- **Giao diện 2 cấp**: Hỗ trợ bộ lọc hai cấp độ trực quan (Chọn Lô ➔ Chọn Mẻ con). Mặc định luôn tự động chọn mẻ con mới nhất trong Lô để hiển thị báo cáo và biểu đồ chất lượng.

---

## 2. Thiết kế Cơ sở dữ liệu nâng cấp (Database Design)

Cấu trúc cơ sở dữ liệu đã được bổ sung cơ chế tự động nâng cấp (Auto-Migration) và đảm bảo an toàn dữ liệu lịch sử.

### 2.1. Cấu trúc bảng `runs` (Bảng mới quản lý Mẻ con)
Bảng này lưu vết thời gian chạy và trạng thái của từng Mẻ con trong một Lô sản xuất:
```sql
CREATE TABLE IF NOT EXISTS `runs` (
  `id` INT AUTO_INCREMENT PRIMARY KEY,
  `batch_id` INT NOT NULL,
  `run_number` INT NOT NULL,
  `name` VARCHAR(150) NOT NULL UNIQUE,
  `status` VARCHAR(50) NOT NULL DEFAULT 'Pending',
  `start_time` DATETIME NULL,
  `end_time` DATETIME NULL,
  `created_at` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  FOREIGN KEY (`batch_id`) REFERENCES `batches`(`id`) ON DELETE CASCADE,
  INDEX `idx_runs_batch` (`batch_id`),
  INDEX `idx_runs_status` (`status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
```

### 2.2. Nâng cấp bảng `batches` (Lô sản xuất)
Thêm cột `total_runs` kiểu `INT NOT NULL DEFAULT 1` đại diện cho số mẻ sản xuất đã được khai báo trước cho Lô đó.

### 2.3. Nâng cấp các bảng log (`alarmreport`, `alarmlog`, `realtime_alarms`)
Thêm cột `runId` kiểu `INT NULL DEFAULT NULL` làm khóa ngoại mềm trỏ tới bảng `runs(id)` để phân tách chính xác log của từng mẻ con. Giữ lại `batchId` để duy trì khả năng tương thích ngược và hỗ trợ truy vấn báo cáo theo cả 2 cấp độ.

### 2.4. Tự động Di chuyển dữ liệu cũ (Historical Migration)
Để đảm bảo hệ thống cũ chạy mượt mà ngay sau khi nâng cấp:
1. Hệ thống tự động tạo 1 mẻ mặc định (`run_number = 1`, tên dạng `{batch_name}-Run01`) trong bảng `runs` cho mỗi lô cũ chưa có mẻ con.
2. Cập nhật lại cột `runId` của các bản ghi sự cố và báo cáo cũ dựa trên liên kết `batchId` sẵn có.

---

## 3. Hướng dẫn sử dụng HTTP API nâng cấp (HTTP API Specification)

Server HTTP tự lưu trữ (Self-hosted HTTP Server) chạy ngầm trên cổng `5500` đã được mở rộng thêm các API phục vụ giao diện lọc 2 cấp độ.

### 3.1. API Tạo Batch và các Mẻ con: `POST /api/batches/create`
API này chấp nhận khai báo trước số lượng mẻ sản xuất trong lô:
- **Body Input (JSON)**:
  ```json
  {
    "device_name": "TX01",
    "runs_count": 2
  }
  ```
- **Response thành công (200 OK - Trả về chi tiết Lô và các Mẻ Pending được sinh sẵn)**:
  ```json
  {
    "success": true,
    "message": "1 batch(es) created successfully with 2 run(s) each",
    "data": [
      {
        "id": 12,
        "name": "TX01-20260601-01",
        "device_name": "TX01",
        "status": "Pending",
        "total_runs": 2,
        "runs": [
          {
            "id": 24,
            "run_number": 1,
            "name": "TX01-20260601-01-Run01",
            "status": "Pending"
          },
          {
            "id": 25,
            "run_number": 2,
            "name": "TX01-20260601-01-Run02",
            "status": "Pending"
          }
        ]
      }
    ]
  }
  ```

### 3.2. API Lấy danh sách Lô: `GET /api/batches`
Lấy danh sách các lô sản xuất của thiết bị phục vụ dropdown cấp 1:
- **Query Params**: `device_name` (Lọc theo máy), `limit` (Mặc định 50).
- **Response**: Trả về danh sách Lô kèm thời gian bắt đầu, kết thúc của lô và tổng số mẻ `total_runs`.

### 3.3. API Lấy danh sách Mẻ thuộc Lô: `GET /api/runs`
Lấy toàn bộ các mẻ sản xuất thuộc về một lô cụ thể phục vụ dropdown cấp 2:
- **Query Params**: `batch_id` (Bắt buộc).
- **Response**: Trả về danh sách các Mẻ con kèm theo thời gian start-end của từng mẻ cụ thể.

---

## 4. Vòng đời Lô - Mẻ trong mã nguồn (Workflow & State Machine)

Hành vi giám sát tự động của hệ thống được cập nhật đồng bộ ở cả `AlarmReportLogger.cs` và `AlarmLogger.cs`:

### 4.1. Kích hoạt Mẻ theo FIFO (First-In, First-Out)
Khi SCADA phát hiện mẻ bắt đầu chạy (`ThoiGianCapLieu > 0` và đang ở `Idle`):
1. Hệ thống quét cơ sở dữ liệu tìm mẻ `Pending` cũ nhất của thiết bị đó.
2. Cập nhật trạng thái mẻ con đó thành `Active`, lưu ID mẻ vào `activeRunId` trong bộ nhớ.
3. Nếu Batch cha của mẻ này đang ở trạng thái `Pending` (chưa chạy mẻ nào trước đó): Cập nhật Batch cha thành `Active` và lưu ID Lô vào `activeBatchId` trong bộ nhớ, gán `start_time` cho Lô.
4. **Cơ chế Tự khắc phục (Fallback)**: Nếu bên thứ ba chưa tạo mẻ qua API mà PLC đã chạy, hệ thống tự động sinh 1 Lô khẩn cấp (`total_runs = 1`) và 1 Mẻ khẩn cấp tương ứng làm `Active` để đảm bảo dữ liệu ghi log alarmlog và alarmreport không bao giờ bị gián đoạn.

### 4.2. Ghi log liên kết đa cấp độ
Trong suốt 8 công đoạn chạy của mẻ con, mọi bản ghi chèn định kỳ vào `alarmreport` hoặc chèn sự cố vượt ngưỡng tức thời vào `realtime_alarms` đều được điền tự động giá trị cột `batchId` = `activeBatchId` và cột `runId` = `activeRunId`.

### 4.3. Đóng Mẻ sản xuất & Hoàn thành Lô
Khi mẻ xả hàng hoàn tất (Công đoạn 8 kết thúc, các tag rung và xả về `0`):
1. Cập nhật mẻ con hiện tại thành `Completed` và gán thời gian `end_time`.
2. Truy vấn đếm số mẻ chưa `Completed` trong Lô cha hiện hành.
3. **Trường hợp tất cả mẻ con đã hoàn thành**: Cập nhật trạng thái Lô cha (`batches`) thành `Completed` và lưu `end_time` cho Lô.
4. **Trường hợp vẫn còn mẻ con chưa chạy**: Giữ nguyên Lô là `Active` để chờ mẻ tiếp theo.
5. Giải phóng các biến ID trong bộ nhớ và quay lại trạng thái `Idle` chờ mẻ tiếp theo.
