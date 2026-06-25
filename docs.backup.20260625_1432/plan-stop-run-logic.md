# Kế hoạch tích hợp thanh ghi Stop/Run vào AlarmReportLogger (Cơ chế Khóa Kép)

Tài liệu này mô tả kế hoạch triển khai chi tiết cơ chế khóa kép (Double-Lock) dựa trên trạng thái thanh ghi thời gian và Stop/Run để kiểm soát chuyển công đoạn và tự động xử lý mẻ lỗi, bù mẻ cho `AlarmReportLogger`.

## 1. Nghiệp vụ thực tế & Thiết kế giải pháp

### A. Cơ chế Khóa Kép (Double-Lock) chuyển công đoạn:
Để tránh việc chuyển nhầm công đoạn khi máy tạm dừng và chạy lại (do thanh ghi thời gian tạm thời bị reset về `0`), hệ thống sẽ áp dụng cơ chế khóa kép:
- **Điều kiện chuyển công đoạn:** Một công đoạn chỉ được chuyển sang công đoạn tiếp theo khi và chỉ khi:
  1. Thanh ghi thời gian của công đoạn hiện tại bằng `0` (và trước đó đã chạy `> 0`).
  2. Thanh ghi thời gian của công đoạn tiếp theo **phải lớn hơn 0** (`> 0`).
  3. Máy đang ở trạng thái chạy (`Stop = 0`).

*Ví dụ chuyển từ Giai đoạn 1 sang Giai đoạn 2:*
- Chỉ chuyển khi: `thoiGianCapLieu == 0` AND `thoiGianTron1 > 0` AND `Stop == 0`.
- Nếu tạm dừng rồi chạy lại, `thoiGianCapLieu` tạm thời về `0` nhưng `thoiGianTron1` vẫn bằng `0` $\rightarrow$ Khóa kép ngăn cản chuyển công đoạn sai.

### B. Cơ chế phát hiện Reset/Abort lập tức:
- Khi máy dừng (`Stop = 1`) và thời gian công đoạn hiện tại đột ngột nhảy về `0` (trong khi trước đó đã chạy `> 0`, biểu thị qua cờ `hasStarted == true` trước khi reset cờ), ta xác định đây là hành động **Reset/Hủy mẻ trên HMI**.
- Hệ thống sẽ ngay lập tức chuyển trạng thái mẻ hiện tại thành `Error`, chèn mẻ mới bù vào Batch và đưa trạng thái về `Idle`.

### C. Cơ chế tự động chạy bù mẻ:
- Khi một mẻ (Run) bị đánh dấu thành `Error` qua hàm `FailActiveBatch()`, hệ thống sẽ:
  - Cập nhật trạng thái Run đó thành `Error`.
  - Tự động lấy số thứ tự mẻ tiếp theo (ví dụ mẻ 3), thêm mới một bản ghi Run có trạng thái `Pending` vào bảng `runs` thuộc cùng `batch_id`.
  - Cập nhật tăng cột `total_runs` trong bảng `batches` thêm 1.
  - Ghi nhận cảnh báo mẻ lỗi có mức độ `Severity = 'ALARM'` vào bảng `realtime_alarms`.

---

## 2. Chi tiết thay đổi Code

### [HinoTools.Data](file:///c:/Users/tanhv/Project/HinoTools.Alarm_27092023_Test/HinoTools.Alarm_27092023_Test/HinoTools.Data/Log/AlarmReportLogger.cs)

#### [MODIFY] [AlarmReportLogger.cs](file:///c:/Users/tanhv/Project/HinoTools.Alarm_27092023_Test/HinoTools.Alarm_27092023_Test/HinoTools.Data/Log/AlarmReportLogger.cs)

- **Thêm Field:**
  ```csharp
  private DateTime? stopStartTime = null;
  ```
- **Thêm Property:**
  ```csharp
  [Category("Hino Settings")]
  [Description("Timeout in seconds before a stopped/paused batch in stage 1-4 is marked as Error (default: 7200s = 2h).")]
  public int StopTimeout { get; set; } = 7200; // 2 giờ
  ```
- **Thêm Hàm `GetSystemTagValue`:**
  - Lấy giá trị tag `Stop` hoặc `Run` từ driver bằng cách ghép tiền tố TaskName động (ví dụ: `AFChemTX01.Stop`).
- **Thêm/Chỉnh sửa Hàm `FailActiveBatch`:**
  - Đánh dấu Run hiện tại là `Error`.
  - Truy vấn lấy thông tin Batch hiện tại, tự động tăng `total_runs` của Batch trong DB và tạo mới một bản ghi Run bù có trạng thái `Pending` thuộc Batch này.
  - Ghi nhận cảnh báo lỗi mẻ vào bảng `realtime_alarms` với mức `Severity = 'ALARM'`.
- **Chỉnh sửa hàm `PollAndLog`:**
  - Đọc các thanh ghi `Stop`, `Run`.
  - Kiểm tra điều kiện `isReset` (HMI nhấn reset) bằng cách so khớp trạng thái `isStopped && hasStarted && time == 0`.
  - Nếu `isReset` $\rightarrow$ ghi nhận `Error` ngay lập tức và gọi `FailActiveBatch()`.
  - Nếu `isStopped` thông thường $\rightarrow$ reset cờ `hasStarted = false` để tránh nhảy công đoạn khi bắt đầu chạy lại, đồng thời theo dõi bộ đếm thời gian dừng máy (timeout 2 tiếng).
  - Nếu đang ở giai đoạn 5 và có `isStopped` $\rightarrow$ kết thúc mẻ thành công.
  - Cập nhật điều kiện chuyển công đoạn trong State Machine sử dụng **Khóa kép**:
    - CD1 $\rightarrow$ CD2: `thoiGianCapLieu == 0 && thoiGianTron1 > 0`
    - CD2 $\rightarrow$ CD3: `thoiGianTron1 == 0 && (thoiGianXaDay > 0 || thoiGianRungXaDay > 0 || thoiGianHutXa > 0)`
    - CD3 $\rightarrow$ CD4: `thoiGianHutXa == 0 && thoiGianTron2 > 0`
    - CD4 $\dots$ CD5: `thoiGianTron2 == 0 && (thoiGianXaHang > 0 || thoiGianRungXaHang > 0)`
  - Cập nhật điều kiện đóng Batch hoàn toàn trong `CompleteActiveBatch` và `FailActiveBatch` dựa trên số lượng mẻ `Pending` hoặc `Active` còn lại bằng `0` (thay vì kiểm tra `!= 'Completed'`).

### [HinoTools.Alarm](file:///c:/Users/tanhv/Project/HinoTools.Alarm_27092023_Test/HinoTools.Alarm_27092023_Test/HinoTools.Alarm/Control/AlarmLogger.cs)

#### [MODIFY] [AlarmLogger.cs](file:///c:/Users/tanhv/Project/HinoTools.Alarm_27092023_Test/HinoTools.Alarm_27092023_Test/HinoTools.Alarm/Control/AlarmLogger.cs)

- **Chỉnh sửa hàm `GetActiveBatchAndRunId`:**
  - Khi một mẻ mới bắt đầu và cần ép đóng mẻ cũ đang Active (do bị lỗi chưa đóng), cập nhật trạng thái mẻ cũ thành `'Error'` (thay vì `'Completed'`).
  - Thực hiện chèn thêm một mẻ bù `'Pending'` và tăng `total_runs` của Batch tương tự như trong `AlarmReportLogger`.
  - Cập nhật điều kiện đóng Batch hoàn chỉnh dựa trên số lượng mẻ `Pending` hoặc `Active` còn lại bằng `0`.

---

## 3. Kế hoạch xác minh (Verification Plan)

### A. Xác minh Biên dịch
- Sử dụng Visual Studio 2019 MSBuild tại đường dẫn `C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe` để biên dịch toàn bộ solution.

### B. Xác minh Thủ công
1. **Test Chạy bù mẻ khi Reset:**
   - Giả lập mẻ 1 chạy đến giai đoạn Trộn 1 (`currentCongDoan = 2`, `thoiGianTron1 = 100`).
   - Thiết lập `Stop = 1` và `thoiGianTron1 = 0` (giả lập nhấn Reset).
   - Xác nhận mẻ 1 chuyển trạng thái thành `Error` lập tức trong DB.
   - Xác nhận hệ thống tự động tạo thêm bản ghi mẻ 3 (`Pending`) thuộc Batch hiện tại.
2. **Test Tạm dừng & Chạy tiếp (Không lỗi chuyển công đoạn nhờ khóa kép):**
   - Giả lập mẻ 2 chạy đến giai đoạn Trộn 1 (`currentCongDoan = 2`, `thoiGianTron1 = 20`).
   - Thiết lập `Stop = 1`.
   - Thiết lập `Stop = 0` và `thoiGianTron1 = 0` (thời điểm chạy lại). Xác nhận hệ thống **không** chuyển nhầm sang Giai đoạn 3 (vì thanh ghi xả đáy vẫn đang = 0).
   - Thiết lập `thoiGianTron1 = 5` (thời gian tăng lên). Xác nhận cờ `hasThoiGianTron1Started` chuyển thành `true`.
   - Thiết lập `thoiGianTron1 = 0` và `thoiGianXaDay = 10` (kết thúc trộn và bắt đầu xả đáy). Xác nhận mẻ chuyển sang Giai đoạn 3 thành công.
