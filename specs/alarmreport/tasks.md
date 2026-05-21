# Kế hoạch triển khai (Implementation Tasks) - Module Alarm & Report
*Dự án: HinoTools.Alarm*

Dưới đây là danh sách các công việc (Task List) cùng **thời gian ước tính (ETA)** được chia thành 4 giai đoạn chính để tiện cho việc báo cáo và theo dõi tiến độ thực thi.

**Tổng thời gian dự kiến:** ~28 giờ làm việc (3.5 ngày)

## Giai đoạn 1: Tính năng Thống kê mẻ trộn (AlarmReportLogger - 30s/lần) - [ETA: 8h]
- [x] **Task 1.1:** Tạo class `AlarmReportLogger` trong project `HinoTools.Data` (thư mục `Log`).
- [x] **Task 1.2:** Xây dựng cơ chế khởi tạo Database tự động cho bảng `alarmreport` với đầy đủ 17 cột dữ liệu + `DeviceName`, `QuyTrinh`, `CongDoanMay`.
- [x] **Task 1.3:** Triển khai Timer định kỳ 30s và cơ chế trích xuất tiền tố `DeviceName` (ví dụ: `AFChemTX01`) từ chuỗi cấu hình.
- [x] **Task 1.4:** Lập trình "State Machine" theo dõi diễn biến các timer để:
  - Bắt đầu ghi log khi `ThoiGianCapLieu` > 0.
  - Tự động nhảy `QuyTrinh` (+1) cho mẻ mới.
  - Ngắt ghi log khi kết thúc mẻ (nhận diện `ThoiGianRungXaHang` nhảy về 0).

## Giai đoạn 2: Tính năng Cảnh báo vượt ngưỡng tức thời (Realtime - 3s/lần) - [ETA: 6h]
- [x] **Task 2.1:** Tạo class `RealtimeThresholdLogger` độc lập với luồng cũ.
- [x] **Task 2.2:** Xây dựng cơ chế khởi tạo bảng `realtime_alarms`.
- [x] **Task 2.3:** Triển khai Timer định kỳ 3 giây để quét các thanh ghi cần giám sát (Nhiệt độ, Áp suất...).
- [x] **Task 2.4:** Lập trình logic kiểm tra ngưỡng (Threshold) hỗ trợ **toán tử tuỳ chỉnh** (`>`, `<`, `=`): Hàm đánh giá biểu thức `Value [Operator] Threshold`. Dữ liệu cấu hình hiện tại sẽ được cấp mặc định là `>`. Khi thoả mãn, thực hiện `INSERT` vào bảng `realtime_alarms`.

## Giai đoạn 3: Nâng cấp cơ chế sự kiện Alarmlog (Khắc phục lỗi log rác) - [ETA: 8h]
- [x] **Task 3.1:** Phân tích class `ValueAlarmTag` và `AlarmTagBase` trong `HinoTools.Alarm.Model`.
- [x] **Task 3.2:** Tạo class mới `ContinuousAlarmTag` (hoặc sửa đổi) để xử lý các thanh ghi timer liên tục:
  - Gọi `OnAlarm` (OccurrenceTime) khi giá trị từ `0` -> `> 0`.
  - Giữ nguyên trạng thái (Bỏ qua) khi giá trị tiếp tục biến thiên `> 0`.
  - Gọi `OffAlarm` (RestoreTime) khi giá trị từ `> 0` -> `0`.
- [x] **Task 3.3:** Tích hợp class mới vào `AlarmTagFactory` để `AlarmServer` nhận diện đúng cấu hình.

## Giai đoạn 4: Kiểm thử và Hoàn thiện (Testing) - [ETA: 6h]
- [x] **Task 4.1:** Tạo hướng dẫn kiểm thử (testing-guide.md) với kịch bản giả lập và câu truy vấn SQL xác minh.
- [ ] **Task 4.2:** Xác minh dữ liệu bảng `alarmreport` ghi nhận đúng chu kỳ 30s và ngưng đúng lúc kết thúc mẻ. *(Cần SCADA + MySQL)*
- [ ] **Task 4.3:** Xác minh bảng `realtime_alarms` sinh bản ghi tức thời khi ép thanh ghi vượt ngưỡng. *(Cần SCADA + MySQL)*
- [ ] **Task 4.4:** Xác minh bảng `alarmlog` (cũ) chỉ sinh 1 bản ghi Alarm duy nhất cho cả quá trình đếm thời gian. *(Cần SCADA + MySQL)*
