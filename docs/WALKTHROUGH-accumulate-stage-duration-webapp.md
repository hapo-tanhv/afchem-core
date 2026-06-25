# Walkthrough - Hiển thị thời gian công đoạn tích lũy realtime trên WebApp

Tài liệu này tổng hợp kết quả triển khai và kiểm thử thực tế của tính năng hiển thị thời gian công đoạn tích lũy realtime trên WebApp, đảm bảo dữ liệu hiển thị chính xác và không bị trôi/reset khi tạm dừng.

---

## 1. Các thay đổi đã thực hiện

### 1.1. WebApp Backend (`OverviewController.cs`)
- Bổ sung cấu trúc dữ liệu `accumulatedValues` trong JSON API `/Overview/GetCurrentBatchStats`.
- Truy vấn lấy giá trị tích lũy lớn nhất (`MAX(CAST(... AS DECIMAL))`) cho 8 cột thời gian mẻ từ bảng `alarmreport` của `runId` hiện tại.
- Tích hợp xử lý ngoại lệ và an toàn dữ liệu `DBNull.Value`.

### 1.2. WebApp Layout (`LayoutMain.js`)
- Tích hợp biến toàn cục `window.headerIsPaused = batchInfo.isPaused === 1`.
- Cập nhật hàm ticker interval 1s: chỉ thực hiện cộng giây chạy thực tế của Header khi máy đang ở trạng thái `RUNNING` và không bị tạm dừng (`!window.headerIsPaused`).

### 1.3. WebApp Frontend Realtime (`OverviewRealtime.js`)
- Khai báo các đối tượng in-memory `jsAccumulatedTimers` và `jsPreviousTimerValues` để quản lý trạng thái đếm giây phía client.
- Triển khai hàm tính delta và tích lũy thời gian thực `getJsAccumulatedValue(...)`.
- Khai báo hàm `resetJsAccumulators()` để làm sạch bộ đếm khi phát hiện chuyển mẻ chạy mới (`activeRunId !== batchInfo.runId`).
- Thay thế hàm gán giá trị thô bằng `updateTimerTag(...)` cho 8 tag thời gian bồn trộn.
- Bổ sung logic tự đồng bộ hóa ngược từ CSDL (Self-healing) sử dụng `Math.max(ClientValue, DbValue)` trong hàm gọi API định kỳ 30s.

---

## 2. Kết quả kiểm thử & Xác minh

### 2.1. Kiểm thử biên dịch
- Sử dụng MSBuild của Visual Studio 2019 biên dịch thành công 100% solution `LongDucProjectTest.sln` mà không có lỗi hay cảnh báo.

### 2.2. Kiểm thử đơn vị (Unit Tests)
1. **C# Accumulator Test (`ConsoleApp.exe`)**:
   - `TestMonotonicGrowth`: Kiểm thử đếm tăng liên tục từ 0 -> 5. Kết quả: **Thành công (5s)**.
   - `TestRegisterResetOnResume`: Kiểm thử khi PLC reset thanh ghi về 0 lúc Resume (14 -> 0 -> 3). Kết quả: **Thành công (17s)**.
   - `TestCommsDropoutNaNHandling`: Kiểm thử khi mất kết nối PLC (NaN). Kết quả: **Thành công (Bảo toàn dữ liệu)**.
   - `TestRecoverySelfHealing`: Kiểm thử tự khôi phục trạng thái từ DB khi khởi động lại logger. Kết quả: **Thành công (Khôi phục 14s và đếm tiếp chính xác)**.
   - **Kết quả chung**: Toàn bộ 4/4 test cases C# chạy thành công.

2. **JavaScript Accumulator Test (`verify_js_accumulator.js`)**:
   - Chạy test script độc lập bằng Node.js giả lập các kịch bản đếm tăng liên tục, tạm dừng reset thanh ghi, và mất kết nối NaN.
   - **Kết quả**: Toàn bộ 3/3 test cases JS vượt qua thành công, in ra dòng chữ: `[SUCCESS] All Javascript accumulator tests passed successfully!`.

### 2.3. Kiểm thử tích hợp thủ công (Manual Integration Tests)
- **Kịch bản F5 (Page Refresh)**:
  - Thiết lập DB có mẻ đang tích lũy công đoạn 1 là 120s. Load lại trang WebApp Overview.
  - Kết quả: Ô hiển thị `#feedingTime` hiển thị ngay lập tức 120 giây (không bị trôi về 0 giây).
- **Kịch bản Tạm dừng (Pause)**:
  - Máy đang chạy công đoạn 2, giá trị tích lũy tăng lên 45s. Nhấn tạm dừng (STOP = 1 và thanh ghi reset về 0).
  - Kết quả: Giao diện bồn trộn giữ nguyên hiển thị 45s, Header dừng ticking, không tăng tiếp.
- **Kịch bản Chuyển mẻ mới**:
  - Khi hoàn thành mẻ chạy cũ và bắt đầu mẻ chạy mới, WebApp nhận diện `runId` thay đổi và tự động gọi `resetJsAccumulators()`.
  - Kết quả: Toàn bộ 8 ô thời gian công đoạn trên bồn trộn quay về 0s và đếm lại từ đầu.
