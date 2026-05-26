# Implementation Plan - Webhook Receiver

Tài liệu này vạch ra kế hoạch triển khai chi tiết cho tính năng bộ nhận Webhook (Webhook Receiver) của dự án `HinoTools.Alarm`. Các nhiệm vụ được chia nhỏ để thực hiện tuần tự và gia tăng, đảm bảo kiểm thử độc lập và kiểm soát hiệu năng từ sớm.

---

## 1. Thiết lập Cơ sở dữ liệu
Nhiệm vụ này đặt nền móng lưu trữ cho toàn bộ hệ thống Webhook bằng cách tạo bảng ghi log dữ liệu thô.

- [x] 1.1 Tạo bảng lưu trữ log Webhook trong cơ sở dữ liệu MySQL
  - Viết câu lệnh tạo cấu trúc bảng ghi log webhook chứa khóa chính tự tăng, thời gian nhận, trạng thái xử lý, lỗi hệ thống và payload thô.
  - Sử dụng kiểu dữ liệu văn bản lớn cho payload để tiếp nhận bất kỳ cấu trúc JSON kích thước lớn nào mà không bị giới hạn.
  - Đăng ký chỉ mục (Index) trên cột trạng thái để tối ưu hóa tốc độ truy vấn quét mẻ sau này.
  - Thực thi câu lệnh trực tiếp trên cơ sở dữ liệu MySQL của môi trường phát triển.
  - _Requirements: 3.3_

---

## 2. Xây dựng Module Tương tác Dữ liệu
Module này xử lý việc lưu dữ liệu Webhook thô vào cơ sở dữ liệu MySQL thông qua component truy cập dữ liệu hiện tại của hệ thống.

- [x] 2.1 Phát triển tác vụ ghi dữ liệu Webhook bất đồng bộ
  - Thiết lập phương thức chèn bản ghi mới chứa JSON payload thô và thời gian hệ thống nhận được vào bảng dữ liệu vừa tạo.
  - Tích hợp lớp truy cập cơ sở dữ liệu sẵn có của hệ thống để thực thi câu lệnh SQL với tham số an toàn (Parameterized Query).
  - Triển khai cơ chế bất đồng bộ sử dụng Task để không làm nghẽn luồng xử lý chính.
  - Quản lý đóng/mở kết nối cơ sở dữ liệu MySQL chặt chẽ để tránh rò rỉ kết nối.
  - _Requirements: 3.1, 3.2_

- [x] 2.2 Xử lý ngoại lệ và ghi nhật ký lỗi kết nối dữ liệu
  - Bắt các lỗi kết nối cơ sở dữ liệu, lỗi cú pháp hoặc timeout khi thực thi SQL chèn dữ liệu.
  - Ghi nhận chi tiết thông tin lỗi và stack trace ra tệp nhật ký gỡ lỗi hệ thống.
  - Đảm bảo lỗi kết nối dữ liệu không gây sập (crash) toàn bộ chương trình chủ.
  - _Requirements: 3.4_

---

## 3. Xây dựng HTTP Web Server tự host
Module này mở một cổng HTTP và duy trì vòng lặp lắng nghe trên luồng nền để tiếp nhận các yêu cầu mạng gửi tới hệ thống.

- [x] 3.1 (P) Thiết lập cấu hình vận hành và khởi chạy HTTP Server
  - Đọc cấu hình địa chỉ IP, cổng (Port) và chuỗi mã khóa bảo mật từ tệp cấu hình của hệ thống.
  - Khởi tạo cổng lắng nghe mạng và đăng ký địa chỉ URL tương ứng.
  - Quản lý vòng đời khởi động (Start) và dừng (Stop) luồng lắng nghe an toàn theo chu kỳ của chương trình chính.
  - Tránh xung đột cổng bằng cách xử lý ngoại lệ và giải phóng tài nguyên cổng khi dịch vụ dừng.
  - _Requirements: 1.1_

- [x] 3.2 (P) Phát triển vòng lặp lắng nghe luồng nền phi chặn
  - Triển khai vòng lặp lắng nghe các yêu cầu HTTP gửi đến trên một luồng nền độc lập.
  - Đảm bảo luồng lắng nghe không làm đóng băng giao diện người dùng hoặc block các luồng dịch vụ chính.
  - Tiếp nhận luồng yêu cầu mạng đồng thời (concurrency) và bàn giao cho bộ phận xử lý mà không bị nghẽn.
  - _Requirements: 4.2_

---

## 4. Xây dựng Bộ xử lý yêu cầu và Xác thực Bảo mật
Thành phần này chịu trách nhiệm kiểm tra tính hợp lệ của gói tin HTTP gửi đến và quyết định cách phản hồi nhanh chóng cho hệ thống gửi.

- [x] 4.1 (P) Triển khai cơ chế Xác thực Token Bảo mật
  - Đọc mã khóa bảo mật được truyền qua HTTP Header tùy chỉnh từ yêu cầu gửi đến.
  - Từ chối các yêu cầu thiếu header bảo mật và phản hồi ngay mã HTTP lỗi chưa xác thực (401).
  - So khớp mã khóa nhận được với giá trị cấu hình bí mật của hệ thống và từ chối nếu không trùng khớp.
  - _Requirements: 2.1, 2.2, 2.3_

- [x] 4.2 (P) Kiểm tra giao thức và đọc payload JSON
  - Kiểm tra phương thức HTTP của yêu cầu gửi đến, từ chối và phản hồi mã lỗi (405) nếu không phải phương thức truyền dữ liệu (POST).
  - Kiểm tra định dạng dữ liệu trong request header, từ chối và phản hồi mã lỗi (415) nếu không phải kiểu JSON.
  - Thiết lập cơ chế đọc dữ liệu thô từ InputStream của yêu cầu với giới hạn kích thước tối đa 5MB để phòng ngừa tấn công làm tràn bộ nhớ.
  - _Requirements: 1.2, 1.3_

- [x] 4.3 Phân luồng bất đồng bộ và phản hồi nhanh thành công
  - Khi payload được đọc thành công và xác thực hợp lệ, đẩy ngay tác vụ ghi cơ sở dữ liệu ngầm vào Thread Pool.
  - Phản hồi ngay lập tức mã HTTP thành công (200 OK) cùng cấu trúc JSON thông báo tiếp nhận thành công cho hệ thống gửi webhook.
  - Đảm bảo việc ghi DB ngầm diễn ra song song và không trì hoãn việc phản hồi HTTP cho bên thứ ba.
  - _Requirements: 1.4, 4.1_

---

## 5. Tích hợp Hệ thống và Kiểm thử liên thông
Giai đoạn cuối cùng kết nối tất cả các thành phần lại với nhau và kiểm tra độ bền vững của hệ thống.

- [x] 5.1 Liên kết các module và cấu hình hệ thống
  - Khai báo các khóa cấu hình mới (IP, Port, Webhook Token) vào file App.config.
  - Tạo thực thể Web Server, Bộ xử lý yêu cầu và Module cơ sở dữ liệu, tiêm các phụ thuộc (dependencies) tương ứng khi Host chính khởi chạy.
  - Khởi động HTTP Server lắng nghe khi khởi động ứng dụng Server.
  - _Requirements: 1.1, 2.3_

- [x] 5.2 Kiểm thử tích hợp giả lập Webhook
  - Viết script hoặc sử dụng công cụ kiểm thử gửi các yêu cầu giả lập webhook (đúng định dạng, sai token, sai method, payload cực lớn).
  - Kiểm thử hiệu năng (Stress test) gửi đồng thời 100 request liên tục để kiểm chứng cơ chế bất đồng bộ phi chặn và khả năng giải phóng Connection MySQL.
  - Kiểm chứng dữ liệu log thô được ghi đầy đủ và chính xác vào bảng MySQL.
  - _Requirements: 1.4, 3.1, 3.2, 4.1, 4.2_
