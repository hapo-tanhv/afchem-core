# Tài liệu Cải tiến Hệ thống Cảnh báo Thời gian thực (realtime_alarms)

Tài liệu này ghi nhận thiết kế và các thay đổi được thực hiện đối với cơ chế lưu trữ cảnh báo của `realtime_alarms` nhằm đáp ứng yêu cầu đồng bộ hoá với giao diện người dùng (phân biệt Trạng thái mẻ, Công đoạn, và mức độ Cảnh báo).

## 1. Yêu cầu và Giải pháp

### Yêu cầu hiển thị UI:
- Hiển thị danh sách cảnh báo thời gian thực lồng ghép trong từng công đoạn của mẻ trộn (Ảnh 1).
- Nhật ký sự kiện tập trung phân biệt 3 mức độ:
  - **INFO**: Ghi nhận thời điểm bắt đầu/hoàn tất các công đoạn (ví dụ: "Bắt đầu cấp liệu", "Bắt đầu trộn lần 1", "Xả liệu hoàn tất").
  - **WARNING**: Các cảnh báo phi nguy hiểm (ví dụ: vượt quá thời gian cài đặt, áp suất cao).
  - **ALARM**: Các báo động nguy hiểm cần xử lý ngay (ví dụ: nhiệt độ bồn trộn vượt ngưỡng).

### Thiết kế cải tiến (Phương án A):
1. **Liên kết máy trạng thái**:
   - `RealtimeThresholdLogger` sẽ được tích hợp một thuộc tính tham chiếu trực tiếp đến `AlarmReportLogger`.
   - Khi xảy ra sự kiện vi phạm ngưỡng, logger sẽ truy vấn trực tiếp từ `AlarmReportLogger` để lấy thông tin: `batchId` (ID mẻ hiện tại), `QuyTrinh` (số thứ tự mẻ), và `CongDoan` (tên công đoạn thân thiện tương ứng).
2. **Mở rộng định dạng cấu hình Collection**:
   - Mở rộng cấu hình mảng `Collection` của `RealtimeThresholdLogger` để chỉ định mức độ và thông điệp hiển thị riêng cho từng thanh ghi:
     `TagName;Alias;Threshold;Operator;Severity;EventMessage`
     *Ví dụ: `AFChemTX01.ApSuat;ApSuat;0.25;>;WARNING;Áp suất đường ống cao hơn ngưỡng cảnh báo`*
3. **Tự động ghi sự kiện INFO**:
   - `AlarmReportLogger` tự động chèn 1 bản ghi sự kiện `INFO` vào bảng `realtime_alarms` mỗi khi máy trạng thái chuyển đổi công đoạn thành công (Bắt đầu cấp liệu, Bắt đầu trộn 1, Bắt đầu xả đáy, v.v.).

---

## 2. Thay đổi Cơ sở dữ liệu

Bảng `realtime_alarms` được cập nhật thêm 4 cột mới để lưu trữ siêu dữ liệu sự kiện:
```sql
ALTER TABLE `realtime_alarms` ADD COLUMN `QuyTrinh` INT NOT NULL DEFAULT 0;
ALTER TABLE `realtime_alarms` ADD COLUMN `CongDoan` VARCHAR(100) NOT NULL DEFAULT '';
ALTER TABLE `realtime_alarms` ADD COLUMN `batchId` INT NULL DEFAULT NULL;
ALTER TABLE `realtime_alarms` ADD COLUMN `Severity` VARCHAR(50) NOT NULL DEFAULT 'ALARM';
```
*(Hệ thống tự động thực hiện migration khi khởi tạo component)*

---

## 3. Nhật ký Thay đổi Code (Changelog)

### [RealtimeThresholdLogger.cs](file:///c:/Users/tanhv/Project/HinoTools.Alarm_27092023_Test/HinoTools.Alarm_27092023_Test/HinoTools.Data/Log/RealtimeThresholdLogger.cs)
- Cập nhật class `ThresholdItem` để lưu trữ thêm thuộc tính `Severity` và `EventMessageTemplate`.
- Bổ sung thuộc tính `AlarmReportLogger` dạng public để liên kết trực tiếp trên Form.
- Viết hàm migration để tự động thêm 4 cột mới (`QuyTrinh`, `CongDoan`, `batchId`, `Severity`) vào bảng MySQL nếu chưa có.
- Cập nhật logic quét `ScanAndLog()` và `InsertRealtimeAlarm()` để:
  - Lấy thông tin mẻ và công đoạn từ `AlarmReportLogger` (nếu có liên kết).
  - Tự động tạo Message thân thiện dựa vào cấu hình `EventMessageTemplate`.

### [AlarmReportLogger.cs](file:///c:/Users/tanhv/Project/HinoTools.Alarm_27092023_Test/HinoTools.Alarm_27092023_Test/HinoTools.Data/Log/AlarmReportLogger.cs)
- Expose các trường máy trạng thái dạng public: `CurrentCongDoan`, `CurrentQuyTrinh`, `ActiveBatchId`.
- Viết thuộc tính `CurrentCongDoanName` để tự động chuyển đổi mã công đoạn (1-5) sang tên tiếng Việt thân thiện, bao gồm cả việc phát hiện tiểu công đoạn (ví dụ: "Rung xả đáy", "Hút xả đáy", "Rung xả hàng") dựa trên các thanh ghi phụ trợ.
- Thêm cơ chế tự động ghi nhật ký `INFO` vào bảng `realtime_alarms` khi có sự thay đổi giữa các công đoạn.
- **Tích hợp ghi nhận INFO cho tiểu công đoạn (T004, T005, T008)**: Trong hàm `PollAndLog()`, bổ sung logic quét giá trị các thanh ghi của tiểu công đoạn xả và xả hàng. Khi phát hiện các thanh ghi `ThoiGianRungXaDay` (>0), `ThoiGianHutXaDay` (>0), hoặc `ThoiGianRungXaHang` (>0) bắt đầu hoạt động, logger sẽ tự động ghi 1 dòng sự kiện `INFO` tương ứng với mã `T004`, `T005`, `T008` vào bảng `realtime_alarms` để đánh dấu bắt đầu chu trình con một cách đồng bộ.
- **Bổ sung chặn trùng lặp INFO**: Tích hợp kiểm tra trùng lặp trong cùng một công đoạn của một mẻ (chỉ ghi tối đa 1 bản ghi `INFO` để tránh hiện tượng nhiễu tín hiệu lặp lại).

### [Cập nhật Cột restore_time & Tự động Đóng Cảnh báo]
- Bảng `realtime_alarms` được mở rộng thêm cột `restore_time` (kiểu dữ liệu `DATETIME NULL`).
- Khi xảy ra sườn lên (Rising Edge), logger ghi nhận cảnh báo mới và lưu trữ ID bản ghi vừa tạo.
- Khi xảy ra sườn xuống (Falling Edge - giá trị đo đạc trở lại ngưỡng an toàn), hệ thống tự động tìm bản ghi cũ theo ID (hoặc fallback theo TagName active gần nhất) và cập nhật cột `restore_time` bằng thời gian hiện tại.

### [Quét Chu kỳ 1 giây Liên tục & Giới hạn Ghi CSDL 30 giây]
- **Timer luôn chạy chu kỳ 1 giây**: Đảm bảo máy trạng thái (`AlarmReportLogger`) quét liên tục với tần suất cao (1000ms) để phát hiện chính xác thời điểm bắt đầu/kết thúc mẻ và thời điểm chuyển đổi giữa các công đoạn mà không bị trễ/lệch pha.
- **Giới hạn Ghi CSDL (Database Throttling)**:
  - Tích hợp bộ đệm thời gian `lastAlarmReportTime` chạy hoàn toàn trên RAM để lọc tần suất ghi dữ liệu báo cáo mẻ trộn (`alarmreport`). Dữ liệu định kỳ vẫn duy trì đúng chu kỳ 30 giây một lần (`PollingInterval`).
  - **Bypass Throttling cho dòng đầu tiên & dòng cuối cùng**: Khi mẻ trộn vừa bắt đầu hoặc khi mẻ trộn hoàn thành (chuyển về trạng thái IDLE), logger sẽ bỏ qua cơ chế lọc 30s và thực hiện ghi dữ liệu lập tức vào MySQL. Điều này đảm bảo mốc thời gian bắt đầu và kết thúc mẻ trên báo cáo chính xác 100%.
  - **Giữ chu kỳ ghi 30s cố định (Lựa chọn 1)**: Khi có sự chuyển tiếp công đoạn ở giữa mẻ, mẻ vẫn duy trì chu kỳ ghi báo cáo đều đặn 30 giây mà không bị reset lệch mốc thời gian chu kỳ.

### [Chuẩn hoá Cột CongDoan sang mã TagNo]
- **Mục tiêu:** Chuyển đổi dữ liệu trong cột `CongDoan` của bảng `realtime_alarms` từ văn bản tiếng Việt sang dạng mã định danh `TagNo` (ví dụ: `T001`, `T002`, `T003`... hoặc `"IDLE"`) để chuẩn hóa CSDL, tối ưu hóa các lệnh truy vấn/JOIN và tương thích tốt hơn với bảng cấu hình `alarmsettings`.
- **Ánh xạ chi tiết:**
  - Trạng thái chờ/Idle -> `"IDLE"`
  - Cấp liệu -> `"T001"` (Timer Cấp Liệu)
  - Trộn 1 -> `"T002"` (Timer Trộn 1)
  - Xả đáy -> `"T003"` (Timer Xả Đáy)
  - Rung xả đáy -> `"T004"` (Timer Rung Xả Đáy)
  - Hút xả đáy -> `"T005"` (Timer Hút Xả Đáy)
  - Trộn 2 -> `"T006"` (Timer Trộn 2)
  - Xả hàng -> `"T007"` (Timer Xả Hàng)
  - Rung xả hàng -> `"T008"` (Timer Rung Xả Hàng)
- **Implementations:**
  - Thêm thuộc tính `CurrentCongDoanCode` vào `AlarmReportLogger.cs` để trả về chính xác mã `TagNo` (hoặc `"IDLE"`) của trạng thái hiện tại dựa trên máy trạng thái và các thanh ghi phụ trợ.
  - Cập nhật hàm ghi log sự kiện `InsertRealtimeInfoEvent` trong `AlarmReportLogger` để lưu mã định danh này.
  - Cập nhật hàm `ScanAndLog()` trong `RealtimeThresholdLogger` để đọc thuộc tính `CurrentCongDoanCode` và lưu trữ mã định danh `TagNo` trực tiếp vào cột `CongDoan` khi có cảnh báo ngưỡng phát sinh.



