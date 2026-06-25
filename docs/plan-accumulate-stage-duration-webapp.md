# Thiết kế kỹ thuật: Hiển thị thời gian công đoạn tích lũy realtime trên WebApp

Tài liệu này mô tả chi tiết phương án thiết kế và giải pháp kỹ thuật để hiển thị thời gian công đoạn tích lũy thực tế trên giao diện WebApp (sơ đồ bồn trộn và Header tổng thời gian), giải quyết triệt để lỗi sụt lùi/reset thời gian hiển thị về 0 giây mỗi khi máy ở trạng thái tạm dừng (Pause) do PLC reset thanh ghi.

---

## 1. Nghiệp vụ & Nguyên tắc hoạt động

Giải pháp sử dụng mô hình **Hybrid Client-Server Accumulator** như sau:
1. **Dữ liệu hạt giống từ CSDL**: API Backend `/Overview/GetCurrentBatchStats` truy vấn các giá trị lớn nhất (MAX) của 8 công đoạn đã lưu trong bảng `alarmreport` của mẻ chạy hiện tại để trả về làm hạt giống (seed) cho client.
2. **Tích lũy phía Client (Delta-ticking)**: Trên trình duyệt, script lắng nghe sự kiện thay đổi giá trị tag từ WCF. Khi giá trị mới đến, nó tính toán khoảng chênh lệch (delta = newValue - prevValue). Nếu delta $\ge 0$, nó cộng dồn vào bộ tích lũy. Nếu delta $< 0$ (PLC reset về 0), nó cộng dồn toàn bộ giá trị mới (nếu $> 0$) để duy trì đếm tiếp.
3. **Đồng bộ hóa Self-Healing**: Định kỳ 30 giây (hoặc khi F5), client gọi API lấy giá trị lớn nhất từ DB và đồng bộ hóa bằng hàm `Math.max(ClientValue, DbValue)` để tránh trôi lệch số liệu hoặc nhảy giật lùi.
4. **Khóa Ticker tổng thời gian**: Khóa bộ ticking 1s tăng giây của Header khi phát hiện cờ tạm dừng máy hoạt động (`isPaused === 1`).

---

## 2. Chi tiết các tệp thay đổi

### 2.1. Backend Controller (`OverviewController.cs`)
- Bổ sung trường `accumulatedValues` trả về qua JSON.
- Thực hiện câu lệnh SQL:
  ```sql
  SELECT 
      MAX(CAST(`ThoiGianCapLieu` AS DECIMAL(10,2))) AS ThoiGianCapLieu,
      MAX(CAST(`ThoiGianTron1` AS DECIMAL(10,2))) AS ThoiGianTron1,
      MAX(CAST(`ThoiGianXaDay` AS DECIMAL(10,2))) AS ThoiGianXaDay,
      MAX(CAST(`ThoiGianRungXaDay` AS DECIMAL(10,2))) AS ThoiGianRungXaDay,
      MAX(CAST(`ThoiGianHutXaDay` AS DECIMAL(10,2))) AS ThoiGianHutXaDay,
      MAX(CAST(`ThoiGianTron2` AS DECIMAL(10,2))) AS ThoiGianTron2,
      MAX(CAST(`ThoiGianXaHang` AS DECIMAL(10,2))) AS ThoiGianXaHang,
      MAX(CAST(`ThoiGianRungXaHang` AS DECIMAL(10,2))) AS ThoiGianRungXaHang
  FROM `alarmreport` 
  WHERE `runId` = {resolvedRunId}
  ```
- Việc chuyển đổi bằng `CAST(... AS DECIMAL(10,2))` là cần thiết vì các cột trong `alarmreport` được lưu dưới dạng `VARCHAR(200)`, tránh lỗi so sánh chuỗi khi lấy `MAX()`.

### 2.2. Ticker Layout Header (`LayoutMain.js`)
- Cập nhật trạng thái `window.headerIsPaused = batchInfo.isPaused === 1` khi nhận phản hồi từ API.
- Cập nhật hàm ticking interval 1s của Header:
  ```javascript
  if (!isOverviewPage && headerMachineStatus === "RUNNING" && !window.headerIsPaused) {
      // Tăng giây chạy thực tế của Header
  }
  ```

### 2.3. Realtime JS Accumulator (`OverviewRealtime.js`)
- Khai báo bộ nhớ tạm `jsAccumulatedTimers` và `jsPreviousTimerValues`.
- Triển khai hàm `getJsAccumulatedValue(stageCode, alias, currentVal)` tính delta và tích lũy.
- Triển khai hàm `resetJsAccumulators()` để đưa các bộ đếm về 0 khi chuyển mẻ mới (`runId` thay đổi).
- Cập nhật callback API `fetchCurrentBatchStats` để đồng bộ `Math.max(ClientValue, DbValue)`.
- Thay thế hàm hiển thị thô `updateTag(...)` bằng hàm tích lũy `updateTimerTag(...)` cho 8 tag thời gian bồn trộn.

---

## 3. Kế hoạch xác minh (Verification Plan)

### 3.1. Unit Testing
- **C#**: Chạy bộ kiểm thử trong `ConsoleApp` để kiểm tra các kịch bản: đếm tăng liên tục, PLC reset khi Resume, lỗi mất kết nối PLC (NaN), và khôi phục dữ liệu từ DB.
- **JavaScript**: Chạy file `verify_js_accumulator.js` qua Node.js để kiểm thử các kịch bản tương tự trên Frontend.

### 3.2. Manual Integration Testing
- Kiểm chứng trực quan việc hiển thị thời gian trên sơ đồ bồn trộn và Header clock trên UI khi chạy thực tế, nhấn nút dừng máy, chạy lại, F5 trang web và chuyển đổi giữa các mẻ.
