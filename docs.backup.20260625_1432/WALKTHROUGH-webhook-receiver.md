# Walkthrough - Webhook Receiver Implementation

Tài liệu này tổng hợp các kết quả triển khai và hướng dẫn vận hành cho tính năng **Webhook Receiver (Bộ nhận Webhook)** trong hệ thống `HinoTools.Alarm`.

---

## 1. Kết quả Triển khai

Chúng ta đã thiết kế và tích hợp thành công bộ nhận Webhook siêu nhẹ chạy trên nền tảng **.NET Framework 4.5** kết hợp cơ sở dữ liệu **MySQL**, đáp ứng đầy đủ các tiêu chuẩn bảo mật, xử lý bất đồng bộ phi chặn và dễ dàng cấu hình.

### Các thành phần mã nguồn đã thay đổi:
1. **[NEW] WebhookHttpServer.cs** (`HinoTools.Data/Http/WebhookHttpServer.cs`):
   - Lớp xử lý Web Server sử dụng `HttpListener` native cực kỳ gọn nhẹ.
   - Triển khai cơ chế xác thực Token bí mật thông qua header `X-Webhook-Token` hoặc Query Parameter (`?token=...`).
   - Tự động tương thích và chấp nhận tất cả các định dạng request (JSON, Form Data, Text) để tránh lỗi Content-Type không tương thích.
   - Giới hạn kích thước payload tối đa 5MB phòng ngừa tấn công làm tràn bộ nhớ.
   - Xử lý bất đồng bộ qua `Task.Run` giúp giải phóng connection MySQL nhanh chóng và phản hồi tức thời `200 OK` cho bên thứ ba.
   - Tự động kiểm tra và khởi tạo bảng `webhook_logs` (Auto-Migration) khi có yêu cầu ghi đầu tiên.

2. **[MODIFY] HinoTools.Data.csproj** (`HinoTools.Data/HinoTools.Data.csproj`):
   - Đăng ký file `WebhookHttpServer.cs` mới vào danh sách biên dịch dự án.

3. **[MODIFY] AlarmReportLogger.cs** (`HinoTools.Data/Log/AlarmReportLogger.cs`):
   - Tích hợp vòng đời khởi chạy/dừng của `WebhookHttpServer` song song cùng `BatchesHttpServer`.
   - Bổ sung các thuộc tính cấu hình trực quan trong Category `"Hino Settings"`:
     - `WebhookPort` (Cổng API Webhook, mặc định `5600`).
     - `WebhookToken` (Mã bảo mật Webhook, mặc định `wh_tok_2f8d9b1e4c7a6e5b3d2c1f0a9e8d7c6b`).
   - Tự động Start Server khi logger được kích hoạt (`TryInitialize`).

4. **[MODIFY] AlarmReportLogger.Designer.cs** (`HinoTools.Data/Log/AlarmReportLogger.Designer.cs`):
   - Bổ sung logic tự động gọi `Stop()` giải phóng cổng mạng của `webhookServer` khi giải phóng component (`Dispose`).

---

## 2. Đặc tả Bảng Cơ sở dữ liệu MySQL

Bảng `webhook_logs` được tự động khởi tạo khi hệ thống bắt đầu tiếp nhận gói tin Webhook hợp lệ đầu tiên:

```sql
CREATE TABLE IF NOT EXISTS `webhook_logs` (
  `id` INT AUTO_INCREMENT PRIMARY KEY,
  `received_at` DATETIME NOT NULL,
  `payload` LONGTEXT NOT NULL,
  `status` VARCHAR(50) NOT NULL DEFAULT 'Pending',
  `error_message` LONGTEXT NULL,
  INDEX `idx_webhook_logs_status` (`status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
```

---

## 3. Hướng dẫn Vận hành & Cấu hình

### 3.1. Thiết lập Cấu hình trong File Ứng dụng
Các tham số cấu hình có thể được thiết lập trực tiếp trên SCADA Designer hoặc khai báo trong file cấu hình ứng dụng (`App.config`):

```xml
<appSettings>
  <!-- Các cấu hình hiện có của HinoTools -->
  <add key="ServerName" value="localhost" />
  <add key="DatabaseName" value="scada" />
  <add key="HttpPort" value="5500" />
  
  <!-- CẤU HÌNH MỚI CHO WEBHOOK RECEIVER -->
  <!-- Cổng mạng lắng nghe API Webhook -->
  <add key="WebhookPort" value="5600" />
  <!-- Mã khóa bảo mật xác thực request gửi tới -->
  <add key="WebhookToken" value="wh_tok_2f8d9b1e4c7a6e5b3d2c1f0a9e8d7c6b" />
</appSettings>
```

- **API Endpoint URL**: `http://[IP_MAY_CHU]:5600/api/webhook?token=wh_tok_2f8d9b1e4c7a6e5b3d2c1f0a9e8d7c6b` (Khuyên dùng cho bên thứ ba truyền trực tiếp qua URL)
  - Hoặc đường dẫn gốc: `http://[IP_MAY_CHU]:5600/api/webhook` (Nếu bên thứ ba có thể cấu hình HTTP Headers)
- **HTTP Method**: `POST`
- **HTTP Headers (Nếu có)**:
  - `Content-Type`: `application/json`
  - `X-Webhook-Token`: `wh_tok_2f8d9b1e4c7a6e5b3d2c1f0a9e8d7c6b` (Nếu dùng phương thức xác thực qua Header)

---

## 4. Hướng dẫn Kiểm thử liên thông (Manual Verification)

Bạn có thể sử dụng các công cụ kiểm thử như **Postman**, **cURL** hoặc Powershell để kiểm chứng hoạt động của API:

### Kiểm thử Thành công (Happy Path)

#### Cách 1: Xác thực qua Query Parameter (Được khuyến nghị cho bên thứ 3)
```bash
curl -X POST "http://localhost:5600/api/webhook?token=wh_tok_2f8d9b1e4c7a6e5b3d2c1f0a9e8d7c6b" \
  -H "Content-Type: application/json" \
  -d '{"event": "batch_start", "batch_name": "TX01-20260526-01", "operator": "User01", "temperature": 75.5}'
```

#### Cách 2: Xác thực qua HTTP Header
```bash
curl -X POST http://localhost:5600/api/webhook \
  -H "Content-Type: application/json" \
  -H "X-Webhook-Token: wh_tok_2f8d9b1e4c7a6e5b3d2c1f0a9e8d7c6b" \
  -d '{"event": "batch_start", "batch_name": "TX01-20260526-01", "operator": "User01", "temperature": 75.5}'
```
* **Kỳ vọng phản hồi**: HTTP Status `200 OK`
  ```json
  {
    "success": true,
    "message": "Webhook payload received"
  }
  ```
* **Kỳ vọng trong DB**: Bản ghi được chèn vào bảng `webhook_logs` với toàn bộ nội dung JSON, trạng thái `Pending` và thời gian nhận chính xác.

### Kiểm thử Thất bại (Error Cases)
1. **Sai Token bảo mật**:
   - Thay đổi header `-H "X-Webhook-Token: SAI_TOKEN"`.
   - *Kỳ vọng phản hồi*: HTTP Status `401 Unauthorized`.
2. **Sai HTTP Method (GET)**:
   - Gửi request `GET http://localhost:5600/api/webhook`.
   - *Kỳ vọng phản hồi*: HTTP Status `405 Method Not Allowed`.

