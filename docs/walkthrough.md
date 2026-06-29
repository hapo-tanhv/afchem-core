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

### 8. Tự động cập nhật `start_time` cho Lô (Batch) khi đổi thứ tự mẻ chạy
- **Vấn đề**: Khi người dùng điều chỉnh thứ tự chạy của các mẻ giữa các lô khác nhau (ví dụ: Batch 1 đang chạy dở dang, nhưng đổi thứ tự chạy cho mẻ 1 của Batch 2 chạy trước), khi thanh ghi cấp liệu hoạt động (`thoiGianCapLieu > 0`), hệ thống kích hoạt mẻ của Batch 2 nhưng cột `start_time` của Batch 2 bị bỏ sót (vẫn bằng `NULL`), do Batch 2 lúc đó có thể không ở trạng thái `Pending` hoặc hệ thống re-link trực tiếp mà không kiểm tra dữ liệu cũ.
- **Giải pháp**:
  - Cập nhật cả [AlarmReportLogger.cs](file:///C:/Users/tanhv/Project/HinoTools.Alarm_27092023_Test/HinoTools.Alarm_27092023_Test/HinoTools.Data/Log/AlarmReportLogger.cs) và [AlarmLogger.cs](file:///C:/Users/tanhv/Project/HinoTools.Alarm_27092023_Test/HinoTools.Alarm_27092023_Test/HinoTools.Alarm/Control/AlarmLogger.cs) khi tìm mẻ chạy tiếp theo:
    - Bổ sung trường `b.start_time` vào các câu lệnh SQL truy vấn mẻ hiện hành (`Active`) và mẻ chờ chạy (`Pending`).
    - Thêm kiểm tra điều kiện: Nếu phát hiện parent Batch đang liên kết có `start_time` bằng `NULL` (chưa được gán thời gian bắt đầu chạy thực tế), hệ thống sẽ lập tức cập nhật `start_time` của Batch đó thành thời điểm hiện tại.
    - Điều này đảm bảo dù mẻ chạy được sắp xếp linh hoạt hay kích hoạt lại sau khi restart ứng dụng, Batch cha luôn có đầy đủ dữ liệu thời gian bắt đầu chạy (`start_time`).
- **Kết quả**: Biên dịch dự án thành công và tích hợp hoạt động đồng bộ trên cả tầng service và ứng dụng chính.

### 9. Cập nhật tỉ lệ chia cho thanh ghi ApSuat và CaiDatApSuat
- **Thay đổi**:
  - Khi đọc dữ liệu thô từ Driver cho hai thanh ghi `ApSuat` (Áp suất thực tế) và `CaiDatApSuat` (Áp suất cài đặt), thay đổi tỉ lệ chia từ `/ 10.0` thành `/ 100.0`.
  - Các thanh ghi môi trường và nhiệt độ khác (`DatNguongNhietDoMoiTruong`, `DatNguongDoAmMoiTruong`, `NhietDoMoiTruong`, `DoAmMoiTruong`, `NhietDoBonTronTren`, `NhietDoBonTronGiua`, `NhietDoBonTronDuoi`) vẫn giữ nguyên tỉ lệ chia `/ 10.0`.
  - **Làm tròn số thập phân**: Áp dụng hàm làm tròn `Math.Round(..., 2)` cho tất cả kết quả tính toán chia tỉ lệ của cả hai nhóm thanh ghi trên để đảm bảo lưu trữ và so sánh ở dạng tối đa 2 chữ số thập phân, tránh sai số biểu diễn dấu phẩy động.
- **Phạm vi cập nhật**:
  - [AlarmReportLogger.cs](file:///C:/Users/tanhv/Project/HinoTools.Alarm_27092023_Test/HinoTools.Alarm_27092023_Test/HinoTools.Data/Log/AlarmReportLogger.cs): Cập nhật trong các hàm `GetScaledValueString`, `GetTagValueByAlias` và `GetSystemTagValue`.
  - [RealtimeThresholdLogger.cs](file:///C:/Users/tanhv/Project/HinoTools.Alarm_27092023_Test/HinoTools.Alarm_27092023_Test/HinoTools.Data/Log/RealtimeThresholdLogger.cs): Cập nhật trong logic xử lý và so sánh ngưỡng cảnh báo thời gian thực.
- **Kết quả**: Biên dịch thành công dự án `HinoTools.Data.csproj` sau khi cập nhật.

### 10. Ghi nhận thời gian tạm dừng và chạy lại của máy vào `realtime_alarms`
- **Yêu cầu**: Khi máy dừng (tạm dừng), ghi nhận sự kiện dừng máy vào bảng `realtime_alarms` với tag name là `System`, mức nghiêm trọng là `INFO`. Khi máy chạy tiếp, cập nhật thời điểm chạy tiếp vào cột `restore_time` của bản ghi đó để phục vụ tính toán thời lượng tạm dừng khi kết xuất Excel.
- **Giải pháp thực hiện**:
  - **Khi máy dừng (`isStopped = true`)**: Thêm hàm `InsertPauseRecord()` để kiểm tra và chèn một bản ghi mới với `TagName = 'System'`, `Severity = 'INFO'`, `Message = 'Tạm dừng máy'` và `restore_time = NULL`. Gắn kèm các thông tin quy trình, công đoạn hiện tại, `batchId`, và `runId` nếu có.
  - **Xác thực thời gian công đoạn**: Chỉ coi trạng thái `Stop = 1` là **tạm dừng** khi có ít nhất 1 trong 8 thanh ghi thời gian công đoạn (`ThoiGianCapLieu`, `ThoiGianTron1`, `ThoiGianXaDay`, `ThoiGianRungXaDay`, `ThoiGianHutXaDay`, `ThoiGianTron2`, `ThoiGianXaHang`, `ThoiGianRungXaHang`) có giá trị `> 0`.
  - **Đồng loạt về 0 (Reset/Shutdown)**: Nếu `Stop = 1` nhưng tất cả 8 thanh ghi trên đồng thời bằng `0`, hệ thống coi đây là hành động **Reset mẻ (hoặc tắt máy)** chứ không phải tạm dừng:
    - Đánh dấu mẻ hiện tại lỗi ngay lập tức (`isReset = true` -> Hủy mẻ và đánh dấu Error trong DB).
    - Tự động đóng sự kiện tạm dừng trước đó nếu có (gán `restore_time = DateTime.Now`).
  - **Khi máy chạy lại (Resumed / Running normally)**: Thêm hàm `UpdatePauseRecord(DateTime resumeTime)` để tìm bản ghi tạm dừng đang hoạt động (`restore_time IS NULL`) của thiết bị và cập nhật `restore_time` thành thời điểm chạy tiếp.
  - **Tự động đóng sự kiện khi kết thúc bất thường**: Cập nhật `UpdatePauseRecord(DateTime.Now)` khi mẻ bị Reset hoặc bị Timeout (2 tiếng) để đảm bảo không bị treo sự kiện tạm dừng trong cơ sở dữ liệu.
  - **Xử lý mẻ mồ côi khi khởi động**: Thêm hàm `ResolveOrphanPauseRecords()` trong hàm `TryInitialize()` để tự động cập nhật `restore_time` cho mọi sự kiện tạm dừng chưa đóng của thiết bị lúc khởi động ứng dụng.
- **Kết quả**: Biên dịch dự án thành công và sẵn sàng hoạt động đồng bộ.

### 11. Thêm cột `is_paused` vào bảng `runs` để phân biệt Tạm dừng trên WebApp
- **Yêu cầu**: WebApp cần phân biệt rõ khi nào một mẻ có `status = 'Active'` đang chạy bình thường và khi nào đang bị tạm dừng (do người vận hành nhấn nút dừng tạm thời).
- **Giải pháp**:
  - **Tự động migration DB**: Thêm kiểm tra trong `EnsureBatchesTableExists()` của [AlarmReportLogger.cs](file:///C:/Users/tanhv/Project/HinoTools.Alarm_27092023_Test/HinoTools.Alarm_27092023_Test/HinoTools.Data/Log/AlarmReportLogger.cs). Khi bắt đầu ứng dụng, nó tự động nâng cấp cấu trúc bảng `runs` bằng cách thêm cột `is_paused` (kiểu `TINYINT NOT NULL DEFAULT 0`) nếu chưa có.
  - **Quản lý vòng đời giá trị `is_paused`**:
    - Khi máy tạm dừng (`Stop = 1` và có ít nhất 1 timer > 0): cập nhật `is_paused = 1` thông qua hàm helper mới `UpdateRunPauseStatus(activeRunId, 1)`.
    - Khi máy chạy lại (`Stop = 0`): cập nhật `is_paused = 0`.
    - Khi mẻ bị reset/lỗi dở dang hoặc kết thúc mẻ: reset `is_paused = 0` trực tiếp trong câu lệnh SQL cập nhật trạng thái của [AlarmReportLogger.cs](file:///C:/Users/tanhv/Project/HinoTools.Alarm_27092023_Test/HinoTools.Alarm_27092023_Test/HinoTools.Data/Log/AlarmReportLogger.cs) và [AlarmLogger.cs](file:///C:/Users/tanhv/Project/HinoTools.Alarm_27092023_Test/HinoTools.Alarm_27092023_Test/HinoTools.Alarm/Control/AlarmLogger.cs).
- **Kết quả**: Biên dịch thành công độc lập cả hai dự án `HinoTools.Data.csproj` và `HinoTools.Alarm.csproj`. WebApp có thể hiển thị trạng thái động bằng cách kiểm tra: `status = 'Active' AND is_paused = 1` (Tạm dừng) hoặc `is_paused = 0` (Đang chạy).

### 12. Sửa lỗi không ghi nhận sự kiện "Bắt đầu cấp liệu" (T001) trong `realtime_alarms`
- **Nguyên nhân**: Khi một mẻ mới được kích hoạt/chuyển trạng thái sang `'Active'` từ phía WebApp, khối lệnh đồng bộ hóa trạng thái mẻ từ database (`activeRunId != dbRunId`) sẽ chạy trước và đồng bộ hóa trực tiếp trạng thái `currentCongDoan = 1` cùng với việc reset flags. Điều này làm cho state machine bỏ qua hoàn toàn block logic `currentCongDoan == 0` (là nơi duy nhất trước đây ghi nhận sự kiện `"Bắt đầu cấp liệu"`).
- **Giải pháp**:
  - Tại khối lệnh đồng bộ hóa `activeRunId != dbRunId` (dòng 325 trong [AlarmReportLogger.cs](file:///C:/Users/tanhv/Project/HinoTools.Alarm_27092023_Test/HinoTools.Alarm_27092023_Test/HinoTools.Data/Log/AlarmReportLogger.cs)), bổ sung gán cờ `hasThoiGianCapLieuStarted = true` và thực hiện gọi hàm `InsertRealtimeInfoEvent("T001", "Bắt đầu cấp liệu")` ngay tại đây.
  - Hàm `InsertRealtimeInfoEvent` có sẵn cơ chế chặn trùng lặp, đảm bảo mỗi mẻ (`runId`) chỉ có tối đa 1 dòng log bắt đầu cấp liệu, tránh việc ghi đúp.
- **Kết quả**: Biên dịch dự án thành công. Sự kiện `"Bắt đầu cấp liệu"` hiện tại đã được ghi nhận đầy đủ và chính xác vào bảng `realtime_alarms` mỗi khi mẻ mới được bắt đầu (cho cả trường hợp kích hoạt từ WebApp và kích hoạt tự động).

### 13. Ghi nhận công đoạn thực tế khi tạm dừng máy và hiển thị/xuất Excel tên công đoạn tương ứng
- **Yêu cầu**: Lưu trữ mã công đoạn thực tế tại thời điểm dừng thay vì lưu giá trị tĩnh `'Tạm dừng'`. Đồng thời, dịch vụ xuất Excel của WebApp cần hiển thị tên tiếng Việt tương ứng thay vì mã thô.
- **Giải pháp**:
  - **Hệ thống Core Logger**: Thay đổi logic ghi trường `CongDoan` trong hàm `InsertPauseRecord()` của `AlarmReportLogger.cs`: thay vì lưu chuỗi `'Tạm dừng'`, hệ thống lưu mã công đoạn thực tế (`CurrentCongDoanCode` ví dụ: `'T001'`, `'T002'`).
  - **Dịch vụ xuất Excel WebApp**: Cập nhật file `BatchRecordExportService.cs` phần sự cố phát sinh. Truy vấn cột `CongDoan` từ `realtime_alarms`, ánh xạ mã công đoạn (`T001` - `T008`) sang tên tiếng Việt tương ứng, và ghép vào mô tả sự cố (ví dụ: `"Tạm dừng máy tại công đoạn Cấp liệu (45s)"`).
- **Kết quả**: Báo cáo Excel và màn hình SCADA hiển thị chính xác tên công đoạn lúc xảy ra sự kiện tạm dừng.

### 14. Tích lũy thời gian chạy thực tế công đoạn khi máy tạm dừng và khôi phục khi khởi động (Self-Healing)
- **Yêu cầu**: Logger C# cần tự động tích lũy thời gian chạy qua các chu kỳ tạm dừng/chạy lại của PLC (do PLC reset timer khi resume) và tự động khôi phục dữ liệu từ DB (Self-Healing) nếu logger bị tắt đột ngột hoặc khởi động lại.
- **Giải pháp**:
  - **Bộ tích lũy phần mềm (Software Accumulator)**: Thêm dictionary `accumulatedTimers` và `previousTimerValues` trong `AlarmReportLogger.cs` để quản lý tích lũy thời gian chạy. 
  - **Tính toán Delta**: Nếu `current >= prev`, cộng `current - prev` vào bộ tích lũy. Nếu `current < prev` (reset), cộng dồn toàn bộ `current` (nếu `current > 0`) vào bộ tích lũy.
  - **Tự khôi phục dữ liệu**: Thêm hàm `RecoverAccumulatorsFromDb(runId)` để truy vấn giá trị lớn nhất đã lưu của 8 cột thời gian mẻ trong bảng `alarmreport` tương ứng với `runId` đang hoạt động, nạp lại vào bộ tích lũy khi logger khởi động hoặc chuyển mẻ mới.
  - **Lọc giá trị NaN**: Trả về `double.NaN` khi mất kết nối PLC và tự động lấy giá trị chu kỳ trước để tránh sai lệch dữ liệu.
- **Kết quả**: Unit tests C# chạy thành công 4/4 kịch bản. Logger ghi nhận chính xác thời gian tích lũy thực tế vào bảng `alarmreport`.

### 15. Tích lũy thời gian công đoạn thực tế realtime trên WebApp (Frontend JS Accumulator & Layout main)
- **Yêu cầu**: WebApp cần hiển thị thời gian công đoạn tích lũy thực tế trên sơ đồ bồn trộn và header tổng thời gian chạy mà không bị nhảy về 0s khi tạm dừng. Đồng bộ hóa với dữ liệu DB khi F5 hoặc polling 30s.
- **Giải pháp**:
  - **API Backend**: Bổ sung từ khóa `accumulatedValues` trong API `GetCurrentBatchStats` (`OverviewController.cs`) để trả về giá trị lớn nhất từ CSDL bằng truy vấn `MAX(CAST(col AS DECIMAL(10,2)))` từ bảng `alarmreport` của `runId` hiện tại.
  - **Client-side JS Accumulator**: Khai báo `jsAccumulatedTimers` and `jsPreviousTimerValues` trong `OverviewRealtime.js`. Áp dụng giải thuật tính delta tương tự backend: `newValue - prevValue` và cộng dồn. Tự động reset accumulators khi chuyển sang mẻ mới (`runId` thay đổi).
  - **Đồng bộ hóa Self-healing**: Trong callback API `GetCurrentBatchStats`, đồng bộ hóa bằng hàm `Math.max(ClientValue, DbValue)` để tránh bộ đếm client nhảy giật lùi.
  - **Ticking Header Clock**: Cập nhật `LayoutMain.js` lưu trữ trạng thái `window.headerIsPaused` và dừng ticking clock của Header khi máy tạm dừng (`isPaused === 1`).
- **Kết quả**: Biên dịch thành công solution WebApp. Unit tests JS chạy thành công 3/3 kịch bản. Thời gian hiển thị mượt mà, chính xác theo giây thực tế.

### 16. Thêm cấp độ cảnh báo AVERAGE (LOW, AVERAGE, HIGH) và sửa đổi logic phân loại cảnh báo
- **Yêu cầu**: 
  - Tích hợp thêm cấp độ cảnh báo Trung bình (`AVERAGE`) vào hệ thống (bên cạnh `LOW` và `HIGH`) để hỗ trợ đầy đủ 3 cấp độ tương ứng với 3 màu sắc Vàng - Cam - Đỏ.
  - Sửa các lỗi logic cảnh báo áp suất, thời gian công đoạn lệch, và động cơ lỗi để khớp với tài liệu tiêu chuẩn.
- **Giải pháp**:
  - **Enum & Server SCADA (`HinoTools.Alarm`)**:
    - Cập nhật enum `AlarmLevel.cs` thành 3 cấp độ: `Low = 0`, `Average = 1`, `High = 2`.
    - Tự động chạy câu lệnh SQL di trú dữ liệu cấu hình khi server khởi động: `UPDATE alarmsettings SET Level = 2 WHERE Level = 1` để nâng cấp các tag cũ từ `High` lên giá trị `2` mới.
    - Cập nhật giao diện hiển thị `AlarmViewer.cs` hỗ trợ màu Cam (`Color.Orange`) và nhấp nháy Cam/Đen cho cấp độ `Average`.
  - **Cảnh báo lệch thời gian công đoạn (`AlarmReportLogger.cs`)**:
    - Sửa logic tính toán chênh lệch thành trị tuyệt đối `Math.Abs(actualDuration - setpoint)` để phát hiện cả lỗi chạy nhanh và chạy chậm.
    - So sánh `deviation > 0` và phân cấp mức độ cảnh báo chính xác theo yêu cầu: lệch dưới 300s báo `LOW` (Vàng), từ 300s-600s báo `AVERAGE` (Cam), trên 600s báo `HIGH` (Đỏ).
  - **Ngăn chặn trùng lặp và phân cấp lỗi áp suất/động cơ (`RealtimeThresholdLogger.cs`)**:
    - Xây dựng cơ chế lọc mức cảnh báo cao nhất (`highestViolatingRankByTag`) cho cùng một `TagName` trong mỗi chu kỳ quét.
    - Nếu có nhiều quy tắc ngưỡng bị vi phạm đồng thời trên một tag (ví dụ: áp suất $<1.5\text{ bar}$ vi phạm cả quy tắc `<2` báo `HIGH` và `<3` báo `AVERAGE`), hệ thống sẽ **chỉ kích hoạt cảnh báo có mức độ nghiêm trọng cao nhất (HIGH)** và tự động đánh dấu phục hồi (resolve) cho cảnh báo mức độ thấp hơn (AVERAGE).
    - Cập nhật cấu hình Collection tĩnh của cảm biến áp suất thành: `<2` báo `HIGH` và `<3` báo `AVERAGE` (sử dụng unique alias bằng chuẩn 5 phần).
    - Nâng cấp cấu hình các mã lỗi động cơ/máy (`MayLoi` từ 1-5) thành mức cảnh báo `HIGH`.
  - **Unit Tests (`ConsoleApp/Program.cs`)**:
    - Cập nhật các hàm test `TestFivePartThresholdParsing` và `TestStageDeviationSeverityMapping` để kiểm tra phân hạng độ lệch thời gian với bộ nhãn mới (`LOW`, `AVERAGE`, `HIGH`), đảm bảo các bài test tự động đều vượt qua.
- **Kết quả**: Biên dịch thành công toàn bộ solution. Unit tests nội bộ hoàn tất thành công 100% (`SUCCESS`).

### 18. Hỗ trợ so sánh động giá trị ngưỡng thực tế với các Tag cài đặt (Setpoint Tags) từ PLC
- **Yêu cầu**: Thay thế các ngưỡng cảnh báo số cứng (ví dụ: `45` cho nhiệt độ môi trường, `75` cho độ ẩm môi trường) bằng việc so sánh động với các Tag cài đặt tương ứng (`DatNguongNhietDoMoiTruong` và `DatNguongDoAmMoiTruong`). Đồng thời, hỗ trợ cơ chế tự động lấy giá trị dự phòng (Fallback) nếu xảy ra lỗi đọc dữ liệu từ PLC và ghi nhận giá trị thực tế của ngưỡng tại thời điểm xảy ra cảnh báo vào CSDL.
- **Giải pháp**:
  - **Cải tiến cú pháp Collection**: Cho phép cấu hình trường ngưỡng dưới dạng `Tên_Tag:Giá_trị_dự_phòng` (ví dụ: `DatNguongNhietDoMoiTruong:45` và `DatNguongDoAmMoiTruong:75`).
  - **Tách bộ phân tích cú pháp (`RealtimeThresholdLogger.cs`)**:
    - Khi khởi tạo, hệ thống phân tích cú pháp chuỗi cấu hình. Nếu phát hiện giá trị phi số, hệ thống tự động nhận diện đây là Tag cài đặt, trích xuất tên Tag và giá trị mặc định dự phòng.
    - Tại thời điểm quét chu kỳ (Scan), hệ thống đọc trực tiếp giá trị thực tế của Tag cài đặt từ PLC, tự động áp dụng công thức chia tỉ lệ (chia `10` đối với nhiệt độ/độ ẩm môi trường), và sử dụng làm ngưỡng động để so sánh.
    - Nếu không đọc được tag cài đặt hoặc tag trả về `NULL`/`NaN` (lỗi kết nối PLC), hệ thống sẽ tự động dùng giá trị mặc định dự phòng đã khai báo.
  - **Ghi nhận CSDL**: Cập nhật hàm `InsertRealtimeAlarm` nhận tham số ngưỡng thực tế được đọc tại thời điểm lỗi xảy ra và ghi nhận giá trị số này vào cột `Threshold` trong bảng `realtime_alarms` thay vì ghi chuỗi tên tag cài đặt.
  - **Unit Tests**: Bổ sung hàm kiểm tra tự động `TestDynamicThresholdParsing` để xác thực cú pháp và logic phân tích ngưỡng động.
- **Kết quả**: Biên dịch và chạy thử thành công. Unit tests pass 100% (`SUCCESS`).


