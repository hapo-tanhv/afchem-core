# Báo cáo Phân tích Tổng quan Dự án: HinoTools.Alarm

## 1. Mục đích của dự án (Project Purpose)
**HinoTools.Alarm** là một ứng dụng Desktop (Windows Forms) được xây dựng trên nền tảng .NET Framework 4.5. 
Mục tiêu chính của dự án là **quản lý cảnh báo (Alarm Management)** thông qua kiến trúc Client-Server sử dụng **WCF (Windows Communication Foundation)**. 
Hệ thống được thiết kế để tích hợp chặt chẽ với các hệ thống công nghiệp và SCADA nhằm giám sát, thu thập, xử lý cảnh báo, sự kiện (events), và ghi log dữ liệu tự động từ các thiết bị.

## 2. Các chức năng chính trong hệ thống (Core Features)

Dựa trên cấu trúc source code và kiến trúc giải pháp, hệ thống đang có các nhóm chức năng chính sau:

### 2.1. Giao tiếp Client-Server (WCF Service)
- **Alarm Service Host (`Host/AlarmHost.cs`)**: Đóng vai trò là Server lắng nghe các kết nối WCF để xử lý cảnh báo.
- **Alarm Client (`Client/AlarmClient.cs`)**: Cung cấp các proxy client kết nối đến WCF Server để gửi/nhận thông tin cảnh báo.
- **Event Handling (`Client/EventClient.cs`)**: Xử lý và truyền tải thông tin sự kiện (events) giữa các node trong hệ thống theo thời gian thực.

### 2.2. Quản lý Cảnh báo & Nghiệp vụ (Alarm Management)
- **Mô hình Dữ liệu (`Model/`)**: Định nghĩa cấu trúc cảnh báo như `AlarmItem` (Đối tượng cảnh báo), `AlarmLevel` (Mức độ nghiêm trọng), `AlarmParam` (Tham số cấu hình).
- **Business Logic (`Service/`)**: Xử lý logic đánh giá, kích hoạt và phân loại các cảnh báo khi có sự cố từ thiết bị gửi về.
- **Email Notification (`Email/`)**: Tích hợp module gửi email thông báo khi có các cảnh báo quan trọng được kích hoạt.

### 2.3. Tương tác Dữ liệu & Ghi Log (Data Layer - `HinoTools.Data`)
- **MySQL Database Connectivity (`Database/DataAccess.cs`)**: Truy cập và tương tác với cơ sở dữ liệu MySQL để lưu trữ thông tin cấu hình và lịch sử cảnh báo.
- **Data Logging (`Log/DataLogger.cs`)**: Ghi log dữ liệu hoạt động của hệ thống liên tục.
- **Energy Logging (`Log/EnergyLogger.cs`)**: Theo dõi và ghi nhận lịch sử tiêu thụ năng lượng của hệ thống công nghiệp.

### 2.4. Giao diện & Trực quan hóa (UI & Visualization)
- **Custom Controls (`Control/`)**: Chứa các component giao diện tùy chỉnh dành cho Windows Forms để hiển thị danh sách cảnh báo.
- **Biểu đồ (Charting)**: Sử dụng thư viện `ZedGraph.dll` để vẽ và hiển thị biểu đồ thống kê xu hướng dữ liệu/cảnh báo.

### 2.5. Khả năng mở rộng (Extensibility)
- **Plugin System**: Hỗ trợ tích hợp thêm các Driver (thông qua `DriverPluginInterface.dll`) cho phép mở rộng khả năng đọc/ghi dữ liệu từ nhiều loại thiết bị phần cứng khác nhau mà không cần sửa code core.

### 2.6. Môi trường Testing tích hợp (Test Environment)
Hệ thống đi kèm các project giả lập giúp developer có thể kiểm thử toàn diện:
- `TestClient` / `TestServer`: Ứng dụng mô phỏng Client/Server qua WCF để test thông tin cảnh báo.
- `TestData` / `ConsoleApp`: Các module test phần truy xuất cơ sở dữ liệu và ghi log.

---
**Kết luận:** 
`HinoTools.Alarm` là một giải pháp middleware quan trọng trong hệ thống SCADA/Công nghiệp. Hệ thống hoạt động độc lập và mạnh mẽ với khả năng thu thập dữ liệu (Logging), theo dõi trạng thái thiết bị theo thời gian thực (WCF), và đưa ra các cảnh báo bằng nhiều hình thức (Giao diện, Email).
