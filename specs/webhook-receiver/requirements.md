# Requirements Document - Webhook Receiver

## Introduction
Tài liệu này định nghĩa các yêu cầu nghiệp vụ và tiêu chí chấp nhận cho tính năng **Webhook Receiver (Bộ nhận Webhook)** trong hệ thống `HinoTools.Alarm`. 
Tính năng này thiết lập một Endpoint HTTP tự host (sử dụng `HttpListener` trên nền tảng .NET Framework 4.5) để tiếp nhận các event trigger (ví dụ: bắt đầu mẻ) từ hệ thống Base bên thứ ba dưới dạng JSON payload. Toàn bộ JSON payload thô sẽ được lưu trữ bất đồng bộ vào một bảng cơ sở dữ liệu MySQL mới (`webhook_logs`) phục vụ cho việc xử lý sau này, đồng thời phản hồi nhanh chóng cho bên gửi để tránh lỗi timeout.

## Requirements

### Requirement 1: Webhook HTTP Endpoint (Tiếp nhận request Webhook)
**Objective:** As a Third-party System (Base System), I want to send POST HTTP requests to the Webhook Endpoint, so that the Webhook Service can receive the event notifications.

#### Acceptance Criteria
1. When a client sends a POST request to the configured webhook URL (e.g., `http://[ip]:[port]/api/webhook/`), the Webhook Service shall parse the request stream and read the raw JSON content.
2. If the request HTTP Method is not POST, the Webhook Service shall reject the request and return an HTTP `405 Method Not Allowed` status code.
3. If the request Content-Type is not `application/json`, the Webhook Service shall reject the request and return an HTTP `415 Unsupported Media Type` status code.
4. When a POST request is processed successfully, the Webhook Service shall return an HTTP `200 OK` status code with a JSON response confirming receipt (e.g., `{"success": true, "message": "Webhook payload received"}`).

---

### Requirement 2: Security & Token Authentication (Xác thực an toàn)
**Objective:** As a Webhook Service Owner, I want to authenticate incoming requests using a pre-configured API Token, so that unauthorized clients cannot submit fake events.

#### Acceptance Criteria
1. When an HTTP request is received at the Webhook Endpoint, the Webhook Service shall extract the security token from the `X-Webhook-Token` HTTP header.
2. If the `X-Webhook-Token` header is missing, the Webhook Service shall reject the request and return an HTTP `401 Unauthorized` status code.
3. If the provided token in the header does not match the configured secret token in `App.config`, the Webhook Service shall reject the request and return an HTTP `401 Unauthorized` status code.

---

### Requirement 3: Database Logging & Storage (Lưu trữ dữ liệu thô)
**Objective:** As a Webhook Service User, I want to store all incoming raw JSON payloads in a MySQL database table, so that we have a persistent history of all triggered events.

#### Acceptance Criteria
1. The Webhook Service shall write to the database using the existing `DataAccess` component via SQL commands.
2. When a valid webhook request is received, the Webhook Service shall save the raw JSON payload, current local timestamp, and initial processing status of `'Pending'` into the `webhook_logs` MySQL database table.
3. The `webhook_logs` table schema shall contain at least: `id` (INT Auto Increment PK), `received_at` (DATETIME), `payload` (LONGTEXT), `status` (VARCHAR(50)), và `error_message` (LONGTEXT).
4. If the database insertion fails, the Webhook Service shall log the error detail to the application logs, and if the client hasn't been responded to yet, return an HTTP `500 Internal Server Error` status code.

---

### Requirement 4: Asynchronous Processing (Xử lý bất đồng bộ)
**Objective:** As a Third-party System (Base System), I want the Webhook Service to respond as quickly as possible, so that our outbound HTTP requests do not time out.

#### Acceptance Criteria
1. When the raw JSON payload is successfully read and validated, the Webhook Service shall hand off the database insertion task to a background task (using `Task.Run` or thread pool) and immediately return the HTTP `200 OK` response to the client.
2. While the background log-saver is processing, the Webhook Service shall ensure that database lockups or network latency spikes do not block incoming HTTP request listener threads.
