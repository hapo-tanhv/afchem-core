# Kế hoạch triển khai cơ chế clone BOM và giới hạn retry cho mẻ bù

## 1. Yêu cầu hệ thống
- **Chỉ clone mẻ thực tế**: Khi một mẻ chạy lỗi (status -> Error), kiểm tra xem mẻ đó có thông tin BOM trong `run_info` hay không. Nếu không có thông tin BOM (mẻ test), bỏ qua không tạo mẻ bù.
- **Giới hạn số lần chạy lại**: Tối đa 3 lần retry cho một mẻ (tổng cộng tối đa 4 bản ghi `runs` có cùng `run_number` trong cùng một Batch).
- **Thứ tự ưu tiên**: Mẻ bù sẽ thừa hưởng `run_number` của mẻ lỗi cũ để sắp xếp ưu tiên chạy trước các mẻ Pending tiếp theo nhờ cơ chế sắp xếp FIFO của query: `ORDER BY b.id ASC, r.run_number ASC`.
- **Đồng bộ thông tin BOM**: Clone toàn bộ các bản ghi trong `run_info` liên kết với mẻ lỗi sang mẻ bù mới.

## 2. Các thay đổi dự kiến

### Component: HinoTools.Data (Log)
#### [MODIFY] [AlarmReportLogger.cs](file:///c:/Users/tanhv/Project/HinoTools.Alarm_27092023_Test/HinoTools.Alarm_27092023_Test/HinoTools.Data/Log/AlarmReportLogger.cs)
- Cập nhật hàm `FailActiveBatch()`:
  - Lấy `failedRunNumber` và `batchName` từ cơ sở dữ liệu.
  - Thực hiện kiểm tra BOM: `SELECT COUNT(*) FROM run_info WHERE run_id = {activeRunId}`. Nếu bằng 0 thì không tạo mẻ bù.
  - Thực hiện kiểm tra số lần retry: `SELECT COUNT(*) FROM runs WHERE batch_id = {batchId} AND run_number = {failedRunNumber}`. Nếu `>= 4` thì không tạo mẻ bù nữa.
  - Chèn mẻ bù với `run_number = failedRunNumber`.
  - Thực hiện query `INSERT INTO run_info ... SELECT ... FROM run_info WHERE run_id = {activeRunId}` để clone toàn bộ BOM.

### Component: HinoTools.Alarm (Control)
#### [MODIFY] [AlarmLogger.cs](file:///c:/Users/tanhv/Project/HinoTools.Alarm_27092023_Test/HinoTools.Alarm_27092023_Test/HinoTools.Alarm/Control/AlarmLogger.cs)
- Cập nhật phần xử lý tạo mẻ bù khi `isNewRunStart == true` trong hàm `GetActiveBatchAndRunId()`:
  - Sử dụng chung logic truy vấn thông tin mẻ cũ: lấy `name` và `total_runs` từ `batches`, đồng thời lấy `run_number` từ `runs` của mẻ bị lỗi (`activeRId`).
  - Kiểm tra xem mẻ lỗi có thông tin BOM trong `run_info` hay không, và kiểm tra giới hạn retry tương tự `AlarmReportLogger`.
  - Thay đổi câu lệnh chèn mẻ bù để gán `run_number = failedRunNumber` (chứ không dùng `newRunNumber` của tên mẻ).
  - Sử dụng `LAST_INSERT_ID()` để lấy ID của mẻ bù mới, sau đó clone toàn bộ thông tin BOM từ `activeRId` sang `newRunId` mới.

## 3. Kế hoạch kiểm thử & Xác minh

### Kiểm thử thủ công:
1. Gửi yêu cầu API tạo Batch `total_runs = 2` (tạo ra Me01, Me02).
2. Kiểm tra xem các bảng `batches`, `runs` và `run_info` đã lưu đầy đủ thông tin BOM hay chưa.
3. Kích hoạt mẻ 1. Giả lập lỗi ở các công đoạn trước công đoạn cuối (bằng cách cho Stop register về 1 và các thanh ghi khác về 0).
4. Xác nhận:
   - Mẻ 1 chuyển sang trạng thái `Error`.
   - Một mẻ mới `Me03` (Pending) được tạo ra với `run_number = 1`, và BOM của nó được clone chính xác từ mẻ 1.
   - Khi chạy lại, mẻ `Me03` sẽ được chọn kích hoạt thành `Active` (chạy trước mẻ `Me02`).
   - Thử nghiệm tiếp tục làm lỗi mẻ `Me03` để sinh `Me04` (retry 2), và lỗi `Me04` sinh `Me05` (retry 3).
   - Nếu `Me05` tiếp tục lỗi, xác nhận không có thêm mẻ clone nào được tạo ra nữa (đạt giới hạn 3 lần retry).
