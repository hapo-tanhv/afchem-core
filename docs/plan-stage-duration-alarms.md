# Thiết kế kỹ thuật: Cảnh báo chênh lệch thời gian công đoạn

Tài liệu này đề xuất phương án và giải pháp chi tiết để triển khai cơ chế cảnh báo chênh lệch thời gian chạy thực tế của công đoạn so với thời gian cài đặt, lưu vào bảng `realtime_alarms`.

---

## 1. Nghiệp vụ & Nguyên tắc hoạt động
Theo lựa chọn của người dùng qua Socratic Gate:
1. **Lưu trữ ngưỡng chênh lệch tối đa**: Lưu tại cột `Value` của bảng cấu hình `alarmsettings` cho các Tag của công đoạn liên quan (kiểu `Type = 2 - Continuous`). Đơn vị tính bằng giây.
2. **Công thức tính độ lệch**: Tính hiệu số tuyệt đối theo giây:
   $$\text{Độ lệch} = | \text{Thời gian Thực tế} - \text{Thời gian Cài đặt} |$$
3. **Kích hoạt cảnh báo**: Cảnh báo ở cả 2 hướng:
   - Thực tế chạy **nhiều hơn** cài đặt (chạy quá lâu).
   - Thực tế chạy **ít hơn** cài đặt (chạy quá nhanh, chưa đạt yêu cầu).

---

## 2. Giải pháp kỹ thuật

### 2.1. Nhận biết thời điểm kết thúc công đoạn (Falling Edge Detection)
Các thanh ghi thời gian của công đoạn (`ThoiGianCapLieu`, `ThoiGianTron1`...) sẽ tăng dần khi công đoạn đang hoạt động, và reset về `0` khi kết thúc để chuyển sang công đoạn kế tiếp.
Chúng ta sẽ lưu trữ giá trị của các thanh ghi này ở chu kỳ trước (`prevCapLieu`, `prevTron1`...). Khi phát hiện giá trị từ $> 0$ nhảy về $0$, đó chính là thời điểm công đoạn vừa chạy xong. Giá trị trước đó chính là **Thời gian chạy thực tế** của công đoạn.

### 2.2. Chi tiết các tệp thay đổi

#### [MODIFY] [AlarmReportLogger.cs](file:///c:/Users/tanhv/Project/HinoTools.Alarm_27092023_Test/HinoTools.Alarm_27092023_Test/HinoTools.Data/Log/AlarmReportLogger.cs)

**Bước 1: Khai báo các trường để lưu giá trị chu kỳ trước (prev values):**
```csharp
private double prevCapLieu = 0;
private double prevTron1 = 0;
private double prevXaDay = 0;
private double prevRungXaDay = 0;
private double prevHutXaDay = 0;
private double prevTron2 = 0;
private double prevXaHang = 0;
private double prevRungXaHang = 0;
```

**Bước 2: Reset các biến này trong hàm `ResetFlags()`:**
```csharp
prevCapLieu = 0;
prevTron1 = 0;
prevXaDay = 0;
prevRungXaDay = 0;
prevHutXaDay = 0;
prevTron2 = 0;
prevXaHang = 0;
prevRungXaHang = 0;
```

**Bước 3: Thêm logic kiểm tra sườn xuống (Falling Edge) vào đầu hàm `PollAndLog()`:**
```csharp
if (activeRunId != null)
{
    if (prevCapLieu > 0 && thoiGianCapLieu == 0) CheckAndLogStageDurationAlarm("T001", prevCapLieu, "ThoiGianCapLieu");
    if (prevTron1 > 0 && thoiGianTron1 == 0) CheckAndLogStageDurationAlarm("T002", prevTron1, "ThoiGianTron1");
    if (prevXaDay > 0 && thoiGianXaDay == 0) CheckAndLogStageDurationAlarm("T003", prevXaDay, "ThoiGianXaDay");
    if (prevRungXaDay > 0 && thoiGianRungXaDay == 0) CheckAndLogStageDurationAlarm("T004", prevRungXaDay, "ThoiGianRungXaDay");
    if (prevHutXaDay > 0 && thoiGianHutXa == 0) CheckAndLogStageDurationAlarm("T005", prevHutXaDay, "ThoiGianHutXaDay");
    if (prevTron2 > 0 && thoiGianTron2 == 0) CheckAndLogStageDurationAlarm("T006", prevTron2, "ThoiGianTron2");
    if (prevXaHang > 0 && thoiGianXaHang == 0) CheckAndLogStageDurationAlarm("T007", prevXaHang, "ThoiGianXaHang");
    if (prevRungXaHang > 0 && thoiGianRungXaHang == 0) CheckAndLogStageDurationAlarm("T008", prevRungXaHang, "ThoiGianRungXaHang");
}
```

**Bước 4: Cập nhật giá trị chu kỳ trước ở cuối hàm `PollAndLog()`:**
```csharp
prevCapLieu = thoiGianCapLieu;
prevTron1 = thoiGianTron1;
prevXaDay = thoiGianXaDay;
prevRungXaDay = thoiGianRungXaDay;
prevHutXaDay = thoiGianHutXa;
prevTron2 = thoiGianTron2;
prevXaHang = thoiGianXaHang;
prevRungXaHang = thoiGianRungXaHang;
```

**Bước 5: Thêm các hàm phụ trợ xử lý so sánh và chèn CSDL:**
* `CheckAndLogStageDurationAlarm`: Lấy cấu hình ngưỡng sai lệch cho phép từ `alarmsettings` và thời gian cài đặt `sp` tương ứng từ `runs`, so sánh và kích hoạt cảnh báo nếu vượt ngưỡng.
* `GetSetpointColumnName`: Ánh xạ mã `TagNo` (T001 - T008) sang cột `sp_...` trong bảng `runs`.
* `GetStageDisplayName`: Lấy tên hiển thị tiếng Việt của công đoạn.
* `InsertRealtimeStageAlarm`: Thực hiện lệnh INSERT cảnh báo vào bảng `realtime_alarms` với mức độ `ALARM`.

---

## 3. Kế hoạch xác minh (Verification Plan)
1. **Kiểm thử biên dịch**: Chạy `MSBuild.exe` để đảm bảo code biên dịch thành công.
2. **Kiểm thử tích hợp**: Viết script test mô phỏng thay đổi giá trị tag chạy thực tế và cài đặt, xác minh xem bản ghi cảnh báo có được sinh ra chính xác trong bảng `realtime_alarms` khi chênh lệch thời gian lớn hơn ngưỡng.
