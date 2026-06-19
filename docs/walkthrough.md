# Walkthrough - Tích hợp thanh ghi Stop/Run và Cơ chế Khóa Kép

Tài liệu này mô tả chi tiết các chỉnh sửa đã được thực hiện để giải quyết bài toán thực tế về mẻ lỗi, bù mẻ và chống chuyển nhầm công đoạn khi máy tạm dừng / chạy lại.

## Các thay đổi đã thực hiện

### 1. [HinoTools.Data.Log.AlarmReportLogger](file:///c:/Users/tanhv/Project/HinoTools.Alarm_27092023_Test/HinoTools.Alarm_27092023_Test/HinoTools.Data/Log/AlarmReportLogger.cs)
- **Thêm thuộc tính cấu hình:**
  - `StopTimeout` (mặc định 7200 giây = 2 tiếng) để tự động đóng mẻ lỗi treo.
- **Thêm cơ chế tự động đọc tag Stop/Run:**
  - Hàm `GetSystemTagValue` tự động tìm kiếm tiền tố nhiệm vụ của driver (ví dụ `AFChemTX01.Stop`) để đọc trực tiếp trạng thái thiết bị mà không cần sửa đổi list cấu hình Collection.
- **Tích hợp cơ chế Khóa Kép (Double-Lock):**
  - Chỉnh sửa `PollAndLog()` để chuyển công đoạn chỉ khi thanh ghi công đoạn hiện tại bằng `0` và thanh ghi công đoạn tiếp theo đã nhảy lên giá trị `> 0`.
  - Reset cờ `hasStarted = false` khi máy dừng (`Stop = 1`) để loại bỏ hoàn toàn khả năng bị chuyển công đoạn sai khi máy chạy lại (lúc mà các thanh ghi thời gian tạm thời bị reset về `0`).
- **Tích hợp cơ chế Reset HMI:**
  - Phát hiện nếu `isStopped` và cờ `hasStarted` đang `true` nhưng thời gian công đoạn hiện tại đột ngột nhảy về `0` $\rightarrow$ xác định đây là Reset, lập tức gọi `FailActiveBatch()`.
- **Tự động sinh mẻ bù:**
  - Triển khai hàm `FailActiveBatch()` để tự động cập nhật mẻ dở dang thành `Error`, tăng `total_runs` trong bảng `batches` lên thêm 1, và chèn bản ghi mẻ `'Pending'` mới để chạy bù.
  - Ghi nhận cảnh báo mẻ lỗi có mức độ `Severity = 'ALARM'` vào bảng `realtime_alarms`.

### 2. [HinoTools.Alarm.Control.AlarmLogger](file:///c:/Users/tanhv/Project/HinoTools.Alarm_27092023_Test/HinoTools.Alarm_27092023_Test/HinoTools.Alarm/Control/AlarmLogger.cs)
- Chỉnh sửa hàm tự động kết thúc mẻ `GetActiveBatchAndRunId` khi có mẻ mới chèn lên:
  - Nếu mẻ cũ bị gián đoạn chưa đóng, cập nhật trạng thái của nó thành `'Error'` (thay vì `'Completed'`).
  - Sinh mẻ bù mới `'Pending'` và tăng `total_runs` của Batch tương tự trong `AlarmReportLogger`.

### 3. [HinoTools.Data.Http.BatchesHttpServer](file:///c:/Users/tanhv/Project/HinoTools.Alarm_27092023_Test/HinoTools.Alarm_27092023_Test/HinoTools.Data/Http/BatchesHttpServer.cs)
- **Cập nhật API `POST /api/batches/create`:**
  - Hỗ trợ tham số `runs` như một bí danh (alias) của `runs_count` trong cả JSON body (ví dụ `{"runs": 4}`) và query string (ví dụ `/api/batches/create?runs=4`).
  - Cho phép người dùng tùy chọn số lượng mẻ (runs) mong muốn trong 1 đợt sản xuất (batch). Mặc định nếu không truyền sẽ là 1.

### 4. Sửa lỗi cập nhật trạng thái cảnh báo (`alarmlog`) và lỗi Parameter Binding
- **Sửa lỗi Parameter Binding trong DataAccess:**
  - Cập nhật hàm `ExecuteNonQuery` có tham số ở cả hai dự án [HinoTools.Alarm.Database.DataAccess](file:///c:/Users/tanhv/Project/HinoTools.Alarm_27092023_Test/HinoTools.Alarm_27092023_Test/HinoTools.Alarm/Database/DataAccess.cs) và [HinoTools.Data.Database.DataAccess](file:///c:/Users/tanhv/Project/HinoTools.Alarm_27092023_Test/HinoTools.Alarm_27092023_Test/HinoTools.Data/Database/DataAccess.cs).
  - Sử dụng `.TrimEnd(',', ';', ')', ']', '\r', '\n')` để cắt bỏ các dấu câu thừa sau tên tham số SQL (như dấu phẩy, dấu ngoặc) khi trích xuất danh sách tên tham số từ câu truy vấn (ví dụ `@Status,` thành `@Status`). Việc này giúp MySQL binding tham số chính xác.
- **Cập nhật trạng thái kết thúc cảnh báo:**
  - Sửa hàm `ActionStatusChanged` trong [HinoTools.Alarm.Server.AlarmServer](file:///c:/Users/tanhv/Project/HinoTools.Alarm_27092023_Test/HinoTools.Alarm_27092023_Test/HinoTools.Alarm/Server/AlarmServer.cs) để cập nhật trạng thái của cảnh báo khi phục hồi thành `"Resolved"` thay vì `"NORMAL"`, đồng bộ hoàn toàn với thiết kế bảng `alarmlog`.

### 5. Cập nhật mức độ nghiêm trọng và khôi phục cảnh báo công đoạn trong `realtime_alarms`
- **Thay đổi Severity thành INFO cho cảnh báo chênh lệch thời gian:**
  - Trong hàm `InsertRealtimeStageAlarm` của [AlarmReportLogger.cs](file:///c:/Users/tanhv/Project/HinoTools.Alarm_27092023_Test/HinoTools.Alarm_27092023_Test/HinoTools.Data/Log/AlarmReportLogger.cs), cập nhật tham số `Severity` khi ghi nhận cảnh báo chênh lệch thời gian thành `"INFO"` thay vì `"ALARM"`.
  - Cập nhật logic check trùng lặp để lọc theo điều kiện `Severity = 'INFO'`.
- **Tự động điền thời gian phục hồi (`restore_time`):**
  - Cập nhật cả 2 hàm ghi log sự kiện/cảnh báo công đoạn trong [AlarmReportLogger.cs](file:///c:/Users/tanhv/Project/HinoTools.Alarm_27092023_Test/HinoTools.Alarm_27092023_Test/HinoTools.Data/Log/AlarmReportLogger.cs) gồm `InsertRealtimeStageAlarm` và `InsertRealtimeErrorEvent` để điền `restore_time = DateTime.Now` trực tiếp lúc ghi, đảm bảo các cảnh báo sự kiện tĩnh này không bị treo ở trạng thái đang hoạt động trên màn hình SCADA.

## Kết quả biên dịch
- Biên dịch thành công toàn bộ solution sử dụng MSBuild 2019.
- Đã chạy kiểm thử tích hợp tự động qua các script test trong thư mục `scratch/`, xác nhận trạng thái cảnh báo trong `alarmlog` được cập nhật chính xác từ `Alarm` sang `Resolved` và ghi nhận đầy đủ `RestoreTime`.
- Xác nhận các cảnh báo chênh lệch thời gian công đoạn được lưu vào `realtime_alarms` với mức độ nghiêm trọng `"INFO"` và điền thời gian `restore_time` tức thời thành công.

### 6. Cơ chế bù mẻ (Compensating Run) - Clone BOM & Thứ tự ưu tiên FIFO
- **Chỉ bù mẻ cho mẻ thực tế (có BOM)**:
  - Hàm `FailActiveBatch()` trong `AlarmReportLogger.cs` và `GetActiveBatchAndRunId()` trong `AlarmLogger.cs` sẽ đếm số lượng bản ghi trong bảng `run_info` tương ứng với `run_id` của mẻ cũ. Nếu bằng 0, hệ thống coi đây là mẻ test và bỏ qua việc tạo mẻ bù.
- **Giới hạn số lần chạy lại (Tối đa 3 lần)**:
  - Hệ thống đếm số lượng mẻ có cùng `run_number` trong Batch hiện tại. Nếu `>= 4` (1 mẻ gốc + 3 lần retry), hệ thống sẽ dừng việc tạo mẻ bù để tránh lặp lỗi vô hạn.
- **Ưu tiên chạy trước nhờ thừa hưởng `run_number`**:
  - Mẻ bù mới được tạo ra ở trạng thái `Pending` và thừa hưởng trực tiếp `run_number` của mẻ lỗi cũ. Câu lệnh truy vấn FIFO lấy mẻ tiếp theo (`ORDER BY b.id ASC, r.run_number ASC`) sẽ tự động chọn mẻ bù này trước các mẻ pending khác trong batch (ví dụ `Me03` có `run_number = 1` sẽ chạy trước `Me02` có `run_number = 2`).
- **Sao chép dữ liệu BOM**:
  - Thực hiện câu lệnh SQL `INSERT INTO run_info ... SELECT ... FROM run_info WHERE run_id = {old_run_id}` để clone toàn bộ chi tiết nguyên vật liệu sang mẻ bù mới.

### 7. Thay đổi công thức tạo tên Batch bằng mã định danh sản phẩm (ma_dinh_danh)
- **Cập nhật WebhookHttpServer.cs & recreate_from_webhook_log.ps1**:
  - Trích xuất tham số `ma_dinh_danh` (hoặc `custom_ma_dinh_danh` làm ưu tiên) từ payload webhook.
  - Thay đổi công thức tạo tên lô (Batch Name) từ dạng tự động tăng số thứ tự (`{device_name}-{date}-{STT:D2}`) sang `{device_name}-{date}-{ma_dinh_danh}` (ví dụ: `TX01-20260619-AF101`).
  - **Cơ chế xử lý trùng lặp (Conflict Resolution)**: Nếu tên lô mới trùng với một lô đã tồn tại trong ngày, hệ thống sẽ tự động tìm kiếm các lô bị trùng và tăng hậu tố (ví dụ: `TX01-20260619-AF101-02`, `TX01-20260619-AF101-03`...).
  - **Cơ chế dự phòng (Fallback)**: Nếu payload không chứa `ma_dinh_danh`, hệ thống sẽ tự động quay về sử dụng số thứ tự tăng dần (`-01`, `-02`...) như cũ.
  - **Đảm bảo tính nhất quán khi tìm số thứ tự**: Cải tiến logic truy vấn cơ sở dữ liệu để tìm đúng số thứ tự tối đa trong các lô có hậu tố số thay vì chỉ lấy bản ghi cuối cùng của ngày (tránh việc parse sai mã định danh chữ thành số).
- **Kết quả kiểm thử**:
  - Đã chạy khôi phục lại database từ log bằng script `scratch/recreate_from_webhook_log.ps1 -Reset`, kiểm chứng các mẻ được sinh ra đúng định dạng: `TX01-20260616-AF101`, `TX01-20260616-AF102`, `TX01-20260619-AF101`.
  - Kiểm thử nhiều lần trùng lặp ghi nhận hậu tố tăng tự động (`-02`, `-03`...) chính xác.
