# Yêu cầu Kỹ thuật: Hệ thống Quản lý Batch (Lô) - Run (Mẻ) và Tích hợp API nâng cấp

Tài liệu này xác định các yêu cầu kỹ thuật và nghiệp vụ cho việc nâng cấp hệ thống Quản lý Mẻ sản xuất. Theo yêu cầu mới nhất từ khách hàng, mô hình liên kết mẻ trộn đã thay đổi từ quan hệ **1-1** (1 Batch = 1 Mẻ) sang **1-N** (1 Batch chứa nhiều Mẻ sản xuất). Mỗi Mẻ sản xuất luôn gồm **8 công đoạn** hoạt động liên tục.

---

## 1. Yêu cầu Cơ sở dữ liệu nâng cấp (Database Schema Upgrade)

Hệ thống cần tự động nâng cấp cấu trúc cơ sở dữ liệu hiện tại thông qua cơ chế **Auto-Migration** ở các component khi khởi động.

### 1.1. Cấu trúc bảng `batches` (Lô sản xuất - Cập nhật)
Bảng `batches` quản lý vòng đời của từng Lô sản xuất:
- **`id`**: `INT AUTO_INCREMENT PRIMARY KEY` - Khóa chính tự tăng.
- **`name`**: `VARCHAR(100) NOT NULL UNIQUE` - Tên Lô, sinh tự động theo quy tắc.
- **`device_name`**: `VARCHAR(100) NOT NULL` - Tên thiết bị thực tế chạy Lô (ví dụ: `TX01`).
- **`status`**: `VARCHAR(50) NOT NULL DEFAULT 'Pending'` - Trạng thái của Lô. Các giá trị hợp lệ:
  - `Pending`: Lô đã tạo trước từ hệ thống/API nhưng chưa bắt đầu bất kỳ mẻ sản xuất nào.
  - `Active`: Lô đang chạy (có ít nhất một mẻ sản xuất đang ở trạng thái `Active`).
  - `Completed`: Lô đã hoàn thành hoàn toàn (tất cả các mẻ con thuộc Lô này đã ở trạng thái `Completed`).
- **`total_runs`**: `INT NOT NULL DEFAULT 1` - Tổng số mẻ sản xuất dự kiến trong Lô này (được khai báo trước từ Base/API).
- **`start_time`**: `DATETIME NULL` - Thời điểm bắt đầu chạy mẻ đầu tiên của Lô.
- **`end_time`**: `DATETIME NULL` - Thời điểm mẻ cuối cùng của Lô hoàn thành.
- **`created_at`**: `TIMESTAMP DEFAULT CURRENT_TIMESTAMP`

### 1.2. Bảng mới `runs` (Mẻ sản xuất - Tạo mới)
Bảng `runs` quản lý chi tiết từng Mẻ sản xuất trong Lô:
- **`id`**: `INT AUTO_INCREMENT PRIMARY KEY` - Khóa chính tự tăng.
- **`batch_id`**: `INT NOT NULL` - Khóa ngoại liên kết tới `batches(id)`.
- **`run_number`**: `INT NOT NULL` - Số thứ tự của mẻ trong Lô (ví dụ: 1, 2, 3...).
- **`name`**: `VARCHAR(150) NOT NULL UNIQUE` - Tên mẻ con, tự động sinh theo quy tắc: `{batch_name}-Run{run_number:D2}` (ví dụ: `TX01-20260601-01-Run01`).
- **`status`**: `VARCHAR(50) NOT NULL DEFAULT 'Pending'` - Trạng thái của mẻ con (`Pending`, `Active`, `Completed`).
- **`start_time`**: `DATETIME NULL` - Thời điểm bắt đầu chạy 8 công đoạn của mẻ con này.
- **`end_time`**: `DATETIME NULL` - Thời điểm hoàn thành 8 công đoạn của mẻ con này.
- **`created_at`**: `TIMESTAMP DEFAULT CURRENT_TIMESTAMP`

### 1.3. Cập nhật các bảng ghi log (`alarmreport`, `alarmlog`, `realtime_alarms`)
Các bảng ghi dữ liệu sự cố và báo cáo cần được liên kết tới cả Lô (Batch) và Mẻ (Run) cụ thể để hỗ trợ tìm kiếm và lọc dữ liệu:
- **`batchId`**: `INT NULL` - Liên kết tới `batches(id)`. Giữ lại để đảm bảo tương thích ngược.
- **`runId`**: `INT NULL` - Liên kết tới `runs(id)`. Thêm mới để quản lý log theo từng mẻ.
- **Cơ chế Auto-Migration**:
  - Khi khởi động, các logger (`AlarmReportLogger`, `AlarmLogger`, `RealtimeThresholdLogger`) kiểm tra sự tồn tại của cột `runId` và bảng `runs`.
  - Nếu chưa tồn tại cột `runId` trong các bảng tương ứng, tự động chạy `ALTER TABLE {table_name} ADD COLUMN runId INT NULL DEFAULT NULL`.
  - Tự động chạy lệnh tạo bảng `runs` nếu chưa có.
  - Tự động chèn cột `total_runs` dạng `INT NOT NULL DEFAULT 1` vào bảng `batches`.

### 1.4. Di chuyển dữ liệu cũ (Historical Data Migration)
Để đảm bảo dữ liệu cũ không bị lỗi khi hiển thị trên giao diện mới:
- Với mỗi bản ghi hiện tại trong bảng `batches` chưa có mẻ con nào trong bảng `runs`:
  - Hệ thống tự động chèn một bản ghi mẻ con vào bảng `runs` với:
    - `batch_id` = `batches.id`
    - `run_number` = 1
    - `name` = `batches.name` (hoặc `batches.name` + `"-Run01"`)
    - `status` = `batches.status`
    - `start_time` = `batches.start_time`
    - `end_time` = `batches.end_time`
  - Cập nhật lại cột `runId` của các bản ghi log cũ trong `alarmreport`, `alarmlog`, và `realtime_alarms` trỏ đến ID mẻ vừa tạo dựa trên giá trị `batchId` hiện tại.

---

## 2. Nâng cấp HTTP API tự lưu trữ (Self-Hosted HTTP Web API)

Hệ thống HTTP API chạy ngầm trên cổng `5500` được nâng cấp để hỗ trợ nghiệp vụ Lô - Mẻ và cung cấp dữ liệu cho giao diện dropdown hai cấp độ (Batch -> Run).

### 2.1. Cập nhật endpoint tạo Batch: `POST /api/batches/create`
- **JSON Body nhận thêm tham số**:
  ```json
  {
    "device_name": "TX01",
    "runs_count": 2
  }
  ```
  - `device_name`: Tên thiết bị (mặc định `"TX01"` nếu trống).
  - `runs_count`: Số mẻ dự kiến trong lô này (mặc định `1`, chấp nhận từ `1` đến `10`).
- **Logic xử lý**:
  - Tạo 1 bản ghi trong bảng `batches` với trạng thái `Pending`, `total_runs` = `runs_count`.
  - Đồng thời tự động chèn `runs_count` bản ghi mẻ con ở trạng thái `Pending` vào bảng `runs` ứng với `batch_id` của Batch vừa tạo.
- **JSON Response**:
  ```json
  {
    "success": true,
    "message": "1 batch with 2 run(s) created successfully",
    "data": {
      "id": 12,
      "name": "TX01-20260601-02",
      "device_name": "TX01",
      "status": "Pending",
      "total_runs": 2,
      "runs": [
        {
          "id": 24,
          "run_number": 1,
          "name": "TX01-20260601-02-Run01",
          "status": "Pending"
        },
        {
          "id": 25,
          "run_number": 2,
          "name": "TX01-20260601-02-Run02",
          "status": "Pending"
        }
      ]
    }
  }
  ```

### 2.2. Điểm cuối lấy danh sách Batch: `GET /api/batches`
- **Query Parameters**:
  - `device_name` (tùy chọn): Lọc theo thiết bị.
  - `limit` (tùy chọn): Số lượng bản ghi trả về tối đa (mặc định `50`).
- **Response**: Trả về danh sách các Batch đã sắp xếp theo thời gian tạo mới nhất.

### 2.3. Điểm cuối lấy danh sách Mẻ của một Batch: `GET /api/runs`
- **Query Parameters**:
  - `batch_id` (Bắt buộc): ID của Lô cần lấy danh sách mẻ con.
- **Response**: Trả về danh sách các Mẻ (`runs`) thuộc Batch đó để phục vụ cho dropdown hiển thị Mẻ trên UI.

---

## 3. Quy trình Tích hợp và Vòng đời Lô - Mẻ (Workflow & Lifecycle)

Mỗi Mẻ sản xuất (Run) sẽ trải qua **8 công đoạn** hoạt động liên tục tương ứng với các trạng thái thanh ghi PLC dưới sự giám sát của `AlarmReportLogger`.

### 3.1. Bắt đầu một Mẻ sản xuất mới (FIFO)
Khi PLC bắt đầu chạy (thanh ghi `ThoiGianCapLieu > 0` và hệ thống đang ở trạng thái Idle `currentCongDoan == 0`):
1. **Xác định thiết bị**: Tách tên thiết bị động từ tên Tag (ví dụ: `AFChemTX01.ThoiGianCapLieu` -> `TX01`).
2. **Truy vấn Mẻ sản xuất tiếp theo (FIFO)**:
   - Tìm Batch có trạng thái `Pending` hoặc `Active` của thiết bị đó có chứa Mẻ con (`runs`) đang ở trạng thái `Pending` có ID nhỏ nhất (cũ nhất).
3. **Kích hoạt Mẻ và Lô**:
   - **Nếu tìm thấy**:
     - Cập nhật trạng thái Mẻ con đó thành `Active`, gán `start_time` là thời gian hiện tại.
     - Lưu ID Mẻ con và ID Lô tương ứng vào bộ nhớ: `activeRunId = run.id`, `activeBatchId = run.batch_id`.
     - Nếu trạng thái của Lô (Batch) cha vẫn là `Pending`: Cập nhật trạng thái Lô thành `Active` và gán `start_time` của Lô bằng thời gian hiện tại (đây là mẻ đầu tiên của lô).
   - **Nếu không tìm thấy bất kỳ mẻ Pending nào**:
     - **Cơ chế Tự phục hồi (Fallback/Emergency)**: Tự động INSERT một Batch mới khẩn cấp (`total_runs` = 1, `status` = `'Active'`, `start_time` = hiện tại) và một Run mới khẩn cấp (`run_number` = 1, `status` = `'Active'`, `start_time` = hiện tại), gán ID của chúng vào bộ nhớ `activeBatchId` và `activeRunId` để đảm bảo dữ liệu ghi log được liên kết thông suốt.

### 3.2. Trong quá trình chạy 8 công đoạn của mẻ
Khi ghi dữ liệu định kỳ vào các bảng `alarmreport`, `alarmlog`, và `realtime_alarms`:
- Hệ thống điền giá trị cột `batchId` = `activeBatchId` và cột `runId` = `activeRunId` hiện hành.

### 3.3. Hoàn thành một Mẻ sản xuất
Khi công đoạn cuối cùng kết thúc hoàn toàn (thỏa mãn đồng thời cả hai giá trị `ThoiGianXaHang == 0` và `ThoiGianRungXaHang == 0` sau khi chạy, chuyển `currentCongDoan` từ 5 về 0):
1. **Cập nhật Mẻ con**: Cập nhật trạng thái Mẻ con (`runs`) hiện tại thành `Completed` và gán `end_time` là thời gian hiện tại.
2. **Kiểm tra hoàn thành Lô (Batch)**:
   - Đếm xem trong Batch cha (`activeBatchId`) còn mẻ sản xuất nào có trạng thái khác `Completed` hay không.
   - **Trường hợp tất cả mẻ con đã hoàn thành**:
     - Cập nhật trạng thái Lô (`batches`) thành `Completed`, gán `end_time` của Lô bằng thời gian hiện tại.
     - Hệ thống ghi nhận Lô sản xuất đã kết thúc trọn vẹn.
   - **Trường hợp vẫn còn mẻ con chưa hoàn thành** (ví dụ: mẻ 1 chạy buổi sáng, mẻ 2 chạy buổi chiều):
     - Lô (`batches`) vẫn giữ nguyên trạng thái `Active`.
     - Hệ thống hiểu rằng Lô này vẫn đang hoạt động và chờ mẻ tiếp theo bắt đầu.
3. **Giải phóng biến bộ nhớ**: Reset `activeRunId = null` và `activeBatchId = null`, đưa hệ thống về trạng thái `Idle` chờ mẻ tiếp theo.

### 3.4. Nhật ký cảnh báo sự cố (`alarmlog`)
Khi phát hiện cảnh báo sự cố từ `AlarmLogger`:
- Tách tên thiết bị động từ Tag sự cố.
- Tìm Mẻ con (`runs`) đang ở trạng thái `Active` ứng với thiết bị đó trong database.
- Nếu tìm thấy, lưu `batchId` và `runId` của mẻ này vào dòng cảnh báo chèn trong bảng `alarmlog`.

---

## 4. Giao diện người dùng: Lọc dữ liệu Hai Cấp độ (Batch -> Run Selection)

Nhằm đáp ứng nghiệp vụ trực quan của khách hàng, màn hình hiển thị báo cáo và đồ thị chất lượng sẽ hoạt động như sau:
1. **Dropdown chọn Batch (Lô)**:
   - Hiển thị danh sách các Batch đã được tạo (sắp xếp theo thời gian mới nhất).
2. **Dropdown chọn Run (Mẻ sản xuất)**:
   - Khi người dùng chọn một Batch ở dropdown 1, dropdown 2 sẽ tự động cập nhật danh sách các Mẻ tương ứng trong Batch đó (đọc từ bảng `runs` qua API `GET /api/runs?batch_id=xxx`).
3. **Cơ chế hiển thị Mặc định (Default Behavior)**:
   - Khi người dùng mới vào trang hoặc chọn một Batch mới: Dropdown Mẻ sẽ **luôn luôn tự động chọn mẻ sản xuất mới nhất** trong Batch đó (Mẻ có `run_number` lớn nhất hoặc mẻ đang `Active`/mới hoàn thành gần đây nhất) để hiển thị dữ liệu báo cáo chất lượng mà không cần người dùng phải bấm chọn thủ công.
