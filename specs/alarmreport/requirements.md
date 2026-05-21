# Phân tích và Yêu cầu: Tính năng ghi log Alarm Report 30s/lần

Dựa trên yêu cầu của bạn, hình ảnh cung cấp và việc phân tích source code hiện tại, đây là phân tích chi tiết về luồng dữ liệu hiện hành và bài toán chúng ta cần giải quyết.

## 1. Phân tích bài toán mới: Ghi log `alarmreport` mỗi 30s
Khách hàng cần theo dõi và thống kê liên tục trạng thái, nhiệt độ, áp suất và thời gian của mẻ trộn theo từng khoảng thời gian cố định, bất kể có xảy ra lỗi hay không, để phục vụ việc truy xuất và hiển thị lên Web Dashboard.

**Bài toán cần xử lý:**
1. **Cơ chế Polling (Định kỳ)**: Timer chạy định kỳ **30 giây** một lần.
2. **Thu thập dữ liệu (Data Gathering)**: Truy xuất vào Driver SCADA (`iDriver`) để đọc giá trị (Value) của 17 thanh ghi theo cấu hình.
3. **Lưu trữ dữ liệu (Data Storage)**: INSERT vào bảng `alarmreport` trong MySQL.

## Giải pháp đề xuất & Cập nhật từ User

Tạo một Component mới tên là `AlarmReportLogger` (trong thư mục `HinoTools.Data/Log/`). Component này sẽ thực hiện các logic sau:

### 1. Phân tích cấu trúc dữ liệu và Bảng `alarmreport`
- Bảng `alarmreport` sẽ là một **Time-series Database** (ghi nhận liên tục mỗi 30s một dòng).
- Cấu trúc cột sẽ map cứng với file Excel thực tế, bao gồm: `ID`, `DateTime`, `TagNo`, `QuyTrinh`, `CongDoanMay`, cùng với tất cả 17 thanh ghi thời gian và nhiệt độ (như `ThoiGianCapLieu`, `ThoiGianTron1`, `NhietDoMay`...).

### 2. Logic xử lý `QuyTrinh` và Nhận diện Hoàn thành Công đoạn
Dựa trên quan sát dữ liệu thực tế:
- **Thanh ghi `CongDoanMay`**: Sẽ trả về các giá trị 1, 2, 3, 4, 5 (tương đương 5 công đoạn của 1 quy trình).
- **Tính toán `QuyTrinh`**: Vì không có thanh ghi `QuyTrinh`, ta sẽ định nghĩa: Một `QuyTrinh` hoàn chỉnh là một chu kỳ máy đi từ Công đoạn 1 đến hết Công đoạn 5. Khi có dấu hiệu bắt đầu mẻ mới (Cấp liệu), `QuyTrinh` sẽ được tăng lên 1 (+1 so với QuyTrinh lớn nhất trong Database).
- **Nhận diện trạng thái Công đoạn dựa trên Timer**: 
  - **Công đoạn 1 (Cấp liệu)**: Bắt đầu khi `ThoiGianCapLieu` > 0. Kết thúc khi `ThoiGianCapLieu` nhảy về 0.
  - **Công đoạn 2 (Trộn 1)**: Bắt đầu khi `ThoiGianTron1` > 0. Kết thúc khi nhảy về 0.
  - **Công đoạn 3 (Xả đáy)**: Gồm 3 thanh ghi (`ThoiGianXaDay`, `ThoiGianRungXaDay`, `ThoiGianHutXa`). Công đoạn này kết thúc khi thanh ghi `ThoiGianHutXa` nhảy về 0.
  - **Công đoạn 4 (Trộn 2)**: Bắt đầu khi `ThoiGianTron2` > 0, kết thúc khi về 0.
  - **Công đoạn 5 (Xả hàng)**: Gồm 2 thanh ghi (`ThoiGianXaHang`, `ThoiGianRungXaHang`). Công đoạn này kết thúc khi thỏa mãn ĐỒNG THỜI 2 điều kiện: `ThoiGianRungXaHang` về 0 VÀ `ThoiGianXaHang` về 0.

### 3. Điều kiện Kích hoạt và Dừng ghi log (Triggering)
- **Bắt đầu**: Khởi động việc ghi log 30s/lần khi phát hiện `ThoiGianCapLieu > 0` (đánh dấu máy bắt đầu chạy công đoạn 1 của mẻ mới).
- **Đang chạy**: Suốt quá trình từ công đoạn 1 đến 5, Timer cứ +30s là đọc toàn bộ 17 thanh ghi và `INSERT` 1 dòng vào database.
- **Kết thúc**: Việc ghi log của mẻ hiện tại sẽ kết thúc khi hoàn tất Công đoạn 5 (ví dụ: các thanh ghi xả hàng về 0). Hệ thống sẽ rơi vào trạng thái chờ (ngừng ghi) cho đến khi mẻ mới cấp liệu lại (`ThoiGianCapLieu > 0`).

### 4. Định danh Máy bằng Tên Thiết Bị (Device Name - ví dụ: "AFChemTX01")
- Như cấu trúc hệ thống, `TagName` đang được lưu dưới dạng `AFChemTX01.CongDoanMay`. Phần tiền tố **"AFChemTX01"** chính là Device Node đại diện cho cỗ máy trộn đó.
- Cần có trường `DeviceName` (hoặc `MachineName`) trong database lưu giá trị này (ví dụ: `"AFChemTX01"`). Nhờ vậy, nếu sau này nhà máy có thêm cụm máy khác (ví dụ "AFChemTX012"), dữ liệu báo cáo 17 thanh ghi sẽ được ghim chính xác cho từng cỗ máy dựa vào tên Device này, rất rõ ràng và chuẩn xác.

## 3. Phân tích bài toán cập nhật cho `alarmlog` (Cơ chế Event-driven)
Cơ chế hiện hành của `alarmlog` đang có điểm bất cập khi xử lý các thanh ghi trả về giá trị liên tục (như bộ đếm thời gian). Nếu giá trị thanh ghi thay đổi (ví dụ: đếm từ 1, 2, 3...) hệ thống lại hiểu nhầm đó là các sự kiện mới, dẫn tới việc liên tục Resolve công đoạn cũ và Insert công đoạn mới.

**Yêu cầu thay đổi logic cho `alarmlog`:**
1. **Định nghĩa lại Trạng thái Alarm cho thanh ghi liên tục**:
   - Một trạng thái cảnh báo/hoạt động được tính là **bắt đầu (OccurrenceTime)** ngay tại thời điểm thanh ghi chuyển từ `0` sang `> 0`.
   - Trong suốt quá trình thanh ghi tiếp tục thay đổi giá trị (nhưng vẫn `> 0`), hệ thống **không được** Insert thêm bản ghi mới hay Resolve bản ghi cũ. Nó vẫn thuộc về cùng một phiên Alarm.
   - Trạng thái chỉ được tính là **kết thúc (RestoreTime / Resolved)** khi giá trị thanh ghi **nhảy về `0`**.
2. **Cập nhật Source Code**: Sẽ cần điều chỉnh lại class xử lý logic của Alarm (ví dụ `ValueAlarmTag` hoặc tạo thêm `ContinuousAlarmTag`) để hỗ trợ toán tử so sánh `> 0` thay vì chỉ so sánh chuỗi (bằng) như hiện tại, và giữ nguyên `Status = ALARM` khi giá trị tiếp tục biến thiên `> 0`.

## 4. Phân tích bài toán Ghi log cảnh báo vượt ngưỡng tức thời (Realtime Threshold Alarm)
Để phục vụ giao diện Web hiển thị các cảnh báo tức thời, hệ thống cần bổ sung một cơ chế giám sát ngưỡng độc lập và nhanh hơn:
- **Cơ chế Polling nhanh**: Một Timer riêng biệt chạy liên tục mỗi 3-4 giây.
- **Điều kiện ghi log**: Đọc giá trị của bất kỳ thanh ghi nào được cấu hình (Nhiệt độ, Áp suất, v.v...). Hệ thống sẽ kiểm tra dựa trên ngưỡng (Threshold) và một tham số Toán tử (Operator: `>`, `<`, `=`). Nếu thỏa mãn biểu thức, hệ thống sẽ lưu log cảnh báo.
- **Lưu trữ dữ liệu**: Hệ thống sẽ INSERT dữ liệu vào một bảng Database hoàn toàn mới (ví dụ: `realtime_alarms`). Việc tách bảng này rất hợp lý để Web truy vấn realtime nhanh chóng mà không làm phình hay lock các bảng `alarmlog` / `alarmreport` hiện tại.
- **Tính tuỳ biến Toán tử**: Chúng ta sẽ **triển khai ngay** khả năng đọc tham số Operator để hệ thống có thể xử lý linh hoạt (VD: nhiệt độ `> 50`, hoặc áp suất `< 10`). Dữ liệu hiện hành sẽ được ưu tiên chạy mặc định với toán tử `>`.

---
Đã hoàn tất phân tích chi tiết. Tiến hành cập nhật file BDD (Behavior) để chuẩn bị code.
