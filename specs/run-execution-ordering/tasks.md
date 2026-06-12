# Implementation Plan

## Tasks

- [x] 1. Database Migration and Setup
- [x] 1.1 Bổ sung logic tự động kiểm tra và tạo cột `execution_order` trong cơ chế migration của Core và HTTP API Server
  - Thêm kiểm tra sự tồn tại của cột `execution_order` trong bảng `runs` của phương thức khởi tạo table
  - Thực thi lệnh `ALTER TABLE` để thêm cột kiểu `INT NOT NULL DEFAULT 0` nếu chưa có
  - _Requirements: 1.1_

- [x] 1.2 Thực hiện di chuyển dữ liệu lịch sử (historical migration) cho các mẻ sản xuất cũ
  - Thực thi câu lệnh cập nhật một lần để gán `execution_order` bằng `id` cho các bản ghi cũ có `execution_order` bằng 0
  - _Requirements: 1.2_

- [x] 2. Core Monitoring Query Logic
- [x] 2.1 (P) Cập nhật logic tìm kiếm mẻ chờ chạy (Pending run) dựa trên thứ tự chạy
  - Sửa đổi câu lệnh SQL truy vấn mẻ con trong `AlarmReportLogger` để sắp xếp ưu tiên theo `execution_order ASC` và tiếp theo là `id ASC`
  - Đảm bảo giữ nguyên các điều kiện lọc theo thiết bị (`device_name`) và trạng thái (`Pending`)
  - _Requirements: 2.1_

- [x] 2.2 (P) Đảm bảo giá trị thứ tự chạy mặc định cho các mẻ con tự động tạo (fallback/emergency)
  - Gán giá trị `execution_order` mặc định bằng 0 khi hệ thống tự động chèn lô và mẻ khẩn cấp do SCADA phát hiện chạy trực tiếp
  - _Requirements: 2.2_

- [x] 3. HTTP API and Webhook Run Creation Logic
- [x] 3.1 (P) Cập nhật endpoint tạo batch trực tiếp để tự động tính toán và gán thứ tự chạy
  - Truy vấn `MAX(execution_order)` của các mẻ hiện hành thuộc thiết bị được yêu cầu trước khi chèn lô mới
  - Gán thứ tự chạy tăng dần liên tiếp từ `MAX + 1` trở đi cho các mẻ con chèn mới trong lô
  - _Requirements: 3.1, 3.2_

- [x] 3.2 (P) Cập nhật tiến trình xử lý Webhook bất đồng bộ để tự động tính toán và gán thứ tự chạy
  - Thay đổi chữ ký hàm xử lý Webhook bất đồng bộ để truyền nhận thêm tham số tên thiết bị (`deviceName`)
  - Truy vấn `MAX(execution_order)` của thiết bị tương ứng trước khi chèn mẻ mới trong luồng xử lý BOM bất đồng bộ
  - _Requirements: 3.1, 3.2_

- [x] 3.3 (P) Cập nhật endpoint truy vấn mẻ con của lô để trả về thông tin thứ tự chạy
  - Trả về trường `execution_order` trong chuỗi JSON kết quả của API `/api/runs?batch_id=xxx`
  - _Requirements: 3.3_

- [x] 4. Verification and Test Execution
- [x] 4.1 Kiểm thử đơn vị (Unit Test) cho logic migration và logic chọn mẻ chạy tiếp theo
  - Viết các test case kiểm tra việc tự động thêm cột và chạy query chọn mẻ Pending có `execution_order` nhỏ nhất
  - _Requirements: 1.1, 1.2, 2.1_

- [x] 4.2 Kiểm thử tích hợp (Integration Test) cho luồng tạo lô/mẻ qua HTTP API và Webhook
  - Thực hiện gọi API trực tiếp và mô phỏng gửi Webhook để xác minh thứ tự chạy được sinh đúng quy luật tăng dần
  - _Requirements: 3.1, 3.2, 3.3_
