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

## Kết quả biên dịch
- Biên dịch thành công dự án `HinoTools.Data` và `HinoTools.Alarm` sử dụng MSBuild 2019.
- *Lưu ý:* Việc biên dịch toàn bộ solution gặp lỗi khóa file PDB do tiến trình ứng dụng `WindowsFormsApp1.exe` và Visual Studio IDE của người dùng đang được khởi chạy và giữ file. Khi chạy thực tế, chỉ cần tắt ứng dụng cũ và mở lại là logic mới sẽ được cập nhật.
