# Implementation Plan - WebApp Accumulate Stage Duration

## Major Task 1: Backend Development
- [ ] 1. Backend Development
- [ ] 1.1 (P) Cập nhật API lấy thông tin mẻ chạy
  - Thực hiện bổ sung truy vấn SQL lấy giá trị lớn nhất (MAX) của 8 công đoạn từ bảng `alarmreport` của `runId` hiện tại.
  - Trả về đối tượng `accumulatedValues` trong JSON của `batchInfo` cho Client.
  - Đảm bảo trả về giá trị mặc định là 0 nếu `runId` không hợp lệ hoặc trống.
  - _Requirements: 1.1, 1.2, 1.3_

## Major Task 2: Frontend Layout Development
- [ ] 2. Frontend Layout Development
- [ ] 2.1 (P) Tinh chỉnh bộ ticker thời gian của Header
  - Đọc thuộc tính `isPaused` từ `batchInfo` và lưu vào trạng thái toàn cục `window.headerIsPaused`.
  - Cập nhật hàm interval ticking 1s của Header để dừng đếm giây khi máy tạm dừng.
  - _Requirements: 4.1, 4.2_

## Major Task 3: Frontend Realtime Development
- [ ] 3. Frontend Realtime Development
- [ ] 3.1 (P) Khởi tạo cache bộ tích lũy trên trình duyệt
  - Định nghĩa biến cache client-side: `jsAccumulatedTimers` và `jsPreviousTimerValues` cho 8 công đoạn.
  - Viết thuật toán tích lũy `getJsAccumulatedValue(stageCode, alias, currentVal)` tính toán delta và cộng dồn.
  - Viết hàm `resetJsAccumulators()` để xóa cache trạng thái đưa về 0.
  - _Requirements: 2.1, 2.2, 2.3, 3.2_
- [ ] 3.2 Tích hợp real-time tags bồn trộn
  - Thay thế hàm `updateTag` thô bằng hàm tùy chỉnh `updateTimerTag` cho 8 tag thời gian công đoạn.
  - Hiển thị giá trị tích lũy trên Mixing Tank Diagram thay vì giá trị thô từ WCF.
  - Đảm bảo đếm tổng thời lượng mẻ chạy cập nhật dựa trên giá trị tích lũy này.
  - _Requirements: 2.4, 3.3_
- [ ] 3.3 Đồng bộ hóa dữ liệu (Self-healing)
  - Cập nhật hàm gọi API polling `fetchCurrentBatchStats` để đồng bộ `jsAccumulatedTimers` từ `batchInfo.accumulatedValues` bằng hàm `Math.max()`.
  - Thêm logic phát hiện mẻ mới để reset bộ đếm khi `runId` thay đổi.
  - _Requirements: 3.1, 3.2_

## Major Task 4: Testing & Verification
- [ ] 4. Testing & Verification
- [ ] 4.1 Kiểm thử đơn vị (Unit Tests)
  - Viết các test case kiểm tra API Backend trả về giá trị tích lũy chính xác.
  - Viết các test case kiểm thử thuật toán tích lũy client-side cho các kịch bản tag tăng, reset về 0, và tiếp tục tăng.
  - _Requirements: 1.1, 1.2, 2.1, 2.2, 2.3_
- [ ] 4.2 Kiểm thử tích hợp hệ thống (Integration Tests)
  - Kiểm thử kịch bản tạm dừng máy (Pause) trên giao diện.
  - Kiểm thử kịch bản F5 tải lại trang để xác thực Self-healing.
  - Kiểm thử kịch bản chuyển tiếp mẻ mới để xác thực reset bộ đếm.
  - _Requirements: 3.1, 3.2, 4.1, 4.2_
