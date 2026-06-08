# Walkthrough - Cảnh báo chênh lệch thời gian công đoạn

Tài liệu này mô tả chi tiết các thay đổi và kết quả thử nghiệm tính năng cảnh báo khi thời gian chạy thực tế của công đoạn chênh lệch vượt quá ngưỡng cho phép so với thời gian cài đặt.

---

## 1. Nghiệp vụ & Nguyên tắc hoạt động
Để đảm bảo chất lượng vận hành và phát hiện sớm các bất thường (chạy quá nhanh, chạy quá lâu, lỗi kẹt cảm biến...), hệ thống tự động so sánh thời gian thực tế chạy của từng công đoạn với cài đặt setpoint tương ứng khi kết thúc công đoạn đó.
- **Ngưỡng cho phép (Threshold):** Được cấu hình trong cột `Value` của bảng cấu hình `alarmsettings` cho các tag công đoạn tương ứng (ví dụ: `T001` - `T008`) với kiểu `Type = 2` (Continuous Timer). Đơn vị tính bằng giây.
- **Công thức tính độ lệch:**
  $$\text{Độ lệch} = | \text{Thời gian Thực tế} - \text{Thời gian Cài đặt} |$$
- **Cơ chế kích hoạt:** Cảnh báo ở cả 2 hướng (chạy quá nhanh hoặc quá lâu) nếu độ lệch lớn hơn ngưỡng cho phép.

---

## 2. Chi tiết các thay đổi đã thực hiện

### [HinoTools.Data.Log.AlarmReportLogger](file:///c:/Users/tanhv/Project/HinoTools.Alarm_27092023_Test/HinoTools.Alarm_27092023_Test/HinoTools.Data/Log/AlarmReportLogger.cs)

1. **Khai báo các trường lưu giá trị chu kỳ trước (prev values):**
   - Khai báo các biến `prevCapLieu`, `prevTron1`, `prevXaDay`, `prevRungXaDay`, `prevHutXaDay`, `prevTron2`, `prevXaHang`, `prevRungXaHang` để theo dõi sườn xuống (transition từ $>0$ về $0$).
2. **Reset cờ và các giá trị chu kỳ trước:**
   - Trong hàm `ResetFlags()`, thực hiện reset toàn bộ các biến `prev... = 0` khi mẻ kết thúc, mẻ lỗi, hoặc khi có mẻ mới để tránh báo động giả khi bắt đầu.
3. **Nhận diện kết thúc công đoạn (Falling Edge Detection):**
   - Trong hàm `PollAndLog()`, sau khi đọc giá trị tag mới, kiểm tra nếu `activeRunId != null` và phát hiện giá trị từ $>0$ nhảy về $0$ (`prev... > 0 && thoiGian... == 0`), tiến hành gọi hàm so sánh `CheckAndLogStageDurationAlarm()`.
   - Cập nhật giá trị chu kỳ trước ở cuối chu kỳ thăm dò.
4. **Logic so sánh & Ghi Log Cảnh Báo:**
   - Triển khai hàm `CheckAndLogStageDurationAlarm()` để:
     - Lấy giá trị cài đặt `Setpoint` tương ứng từ bảng `runs` theo runId hiện tại.
     - Lấy cấu hình ngưỡng `Value` và `TagName` từ bảng `alarmsettings` theo `TagNo` của công đoạn.
     - So sánh hiệu số tuyệt đối. Nếu vượt ngưỡng, gọi `InsertRealtimeStageAlarm()`.
   - Triển khai hàm `InsertRealtimeStageAlarm()` thực hiện `INSERT` cảnh báo vào bảng `realtime_alarms` với mức độ `ALARM` và định dạng thông tin rõ ràng.

---

## 3. Kết quả Kiểm thử Tích hợp (Integration Test)

Tất cả các kịch bản thử nghiệm đã được tự động hóa thông qua script [test_stage_duration_alarms.ps1](file:///c:/Users/tanhv/Project/HinoTools.Alarm_27092023_Test/HinoTools.Alarm_27092023_Test/scratch/test_stage_duration_alarms.ps1).

### Cấu hình thiết lập thử nghiệm:
- **Thiết bị:** `TX01`
- **Công đoạn kiểm thử:** Cấp liệu (`T001` - `AFChemTX01.ThoiGianCapLieu`)
- **Thời gian cài đặt (Setpoint):** 10 giây
- **Ngưỡng lệch cho phép (Threshold):** 5 giây

### Kịch bản thử nghiệm:
1. **Trường hợp 1 (Chạy quá lâu):** Thực tế chạy 20 giây (Lệch $20 - 10 = 10\text{s} > 5\text{s}$).
   - *Kết quả:* **ALARM kích hoạt** thành công.
2. **Trường hợp 2 (Chạy đạt yêu cầu):** Thực tế chạy 12 giây (Lệch $|12 - 10| = 2\text{s} \le 5\text{s}$).
   - *Kết quả:* **Không kích hoạt cảnh báo** (Hoàn toàn chính xác).
3. **Trường hợp 3 (Chạy quá nhanh / Thiếu thời gian):** Thực tế chạy 3 giây (Lệch $|3 - 10| = 7\text{s} > 5\text{s}$).
   - *Kết quả:* **ALARM kích hoạt** thành công.

### Dữ liệu ghi nhận trong bảng `realtime_alarms`:

| DateTime | DeviceName | TagName | Value | Threshold | Message | Severity |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `2026-06-08 11:07:31` | `TX01` | `AFChemTX01.ThoiGianCapLieu` | `20` | `5` | `[Cảnh báo] Giai đoạn Cấp liệu có thời gian thực tế (20s) chênh lệch vượt ngưỡng cho phép (5s) so với cài đặt (10s).` | `ALARM` |
| `2026-06-08 11:07:31` | `TX01` | `AFChemTX01.ThoiGianCapLieu` | `3` | `5` | `[Cảnh báo] Giai đoạn Cấp liệu có thời gian thực tế (3s) chênh lệch vượt ngưỡng cho phép (5s) so với cài đặt (10s).` | `ALARM` |

---

## 4. Hướng dẫn sử dụng & Vận hành

1. **Cấu hình ngưỡng lệch:**
   - Để cài đặt ngưỡng cảnh báo lệch thời gian cho công đoạn, cập nhật cột `Value` trong bảng `alarmsettings` cho dòng cấu hình của công đoạn đó (Type = 2).
   - Ví dụ, để đặt ngưỡng lệch cho trộn lần 1 (`T002`) là 10 giây:
     ```sql
     UPDATE alarmsettings SET Value = '10' WHERE TagNo = 'T002' AND TagName LIKE '%TX01%';
     ```
2. **Theo dõi cảnh báo:**
   - Hệ thống SCADA hoặc giao diện Web giám sát có thể đọc trực tiếp các cảnh báo này từ bảng `realtime_alarms` lọc theo `Severity = 'ALARM'`.

---

## 5. Cơ chế chống ghi trùng lặp khi khởi động lại ứng dụng (Duplicate Prevention on Restart)

Để khắc phục hiện tượng ghi trùng lặp dữ liệu vào các bảng `alarmlog` và `realtime_alarms` khi ứng dụng WinForms bị tắt/mở lại trong lúc công đoạn đang chạy dở dang, hệ thống đã được trang bị cơ chế tự động truy vết dữ liệu cũ:

### 5.1. Xử lý trong bảng `alarmlog` ([AlarmLogger.cs](file:///c:/Users/tanhv/Project/HinoTools.Alarm_27092023_Test/HinoTools.Alarm_27092023_Test/HinoTools.Alarm/Control/AlarmLogger.cs))
- **Khi nhận sự kiện `Alarm`:** Trước khi ghi cảnh báo mới, hệ thống truy vấn DB xem có cảnh báo nào của cùng `TagName` và `runId` hiện tại đang ở trạng thái active hay không (`Status = 'Alarm'` hoặc `RestoreTime IS NULL`). Nếu đã có, hệ thống bỏ qua không chèn trùng lặp, đồng thời nhận diện đây là sự kiện restart chứ không phải bắt đầu mẻ mới (không đánh dấu lỗi mẻ cũ).
- **Khi nhận sự kiện `Resolved`:** Hệ thống kiểm tra xem có dòng cảnh báo active nào tương ứng trong DB hay không. Nếu có, nó sẽ lấy đúng `ID` (GUID cũ) đó để thực hiện lệnh `UPDATE` cập nhật thời gian phục hồi `RestoreTime` và đổi trạng thái thành `Resolved`, giúp toàn bộ vòng đời cảnh báo chỉ nằm trên duy nhất 1 dòng.

### 5.2. Xử lý trong bảng `realtime_alarms` ([AlarmReportLogger.cs](file:///c:/Users/tanhv/Project/HinoTools.Alarm_27092023_Test/HinoTools.Alarm_27092023_Test/HinoTools.Data/Log/AlarmReportLogger.cs))
- Hệ thống thực hiện đếm kiểm tra xem đã tồn tại cảnh báo active nào (`Severity = 'ALARM'` và `restore_time IS NULL`) cho cùng `runId` và tag/công đoạn hay chưa. Nếu đã tồn tại cảnh báo hoạt động chưa phục hồi, hệ thống sẽ tự động bỏ qua lệnh `INSERT` trùng lặp.
- Chỉ khi cảnh báo cũ đã được phục hồi (`restore_time IS NOT NULL`), hệ thống mới cho phép chèn thêm bản ghi cảnh báo mới nếu sự cố lặp lại.

### 5.3. Kết quả thử nghiệm chống trùng lặp ([test_duplicate_check.ps1](file:///c:/Users/tanhv/Project/HinoTools.Alarm_27092023_Test/HinoTools.Alarm_27092023_Test/scratch/test_duplicate_check.ps1))
- **Kịch bản kiểm thử:** Gửi liên tiếp 2 tín hiệu Alarm (giả lập restart) và 1 tín hiệu Resolve.
- **Kết quả alarmlog:** Chỉ ghi nhận duy nhất 1 dòng với ID gốc. Khi Resolve gửi lên, dòng này được cập nhật đầy đủ `RestoreTime` và đổi trạng thái sang `Resolved`.
- **Kết quả realtime_alarms:** Chỉ ghi nhận duy nhất 1 dòng cảnh báo lỗi `System` trong mẻ chạy.

