# Research & Design Decisions - Webhook Receiver

---
**Purpose**: Ghi nhận các kết quả nghiên cứu, đánh giá kiến trúc và các quyết định thiết kế kỹ thuật cho tính năng bộ nhận Webhook (Webhook Receiver) trên nền tảng .NET Framework 4.5 và MySQL.
---

## Summary
- **Feature**: `webhook-receiver`
- **Discovery Scope**: New Feature (Full Discovery)
- **Key Findings**:
  - Hệ thống hiện tại sử dụng **.NET Framework v4.5** legacy và tự host các dịch vụ giao tiếp bằng WCF (`ServiceHost` qua net.tcp). Không có IIS hay ASP.NET MVC/Web API sẵn có trong project chính.
  - Cơ sở dữ liệu đang dùng là **MySQL** thông qua component tự viết `DataAccess` hỗ trợ thực thi SQL thô và truyền tham số (`MySqlParameter`).
  - Giao thức HTTP Webhook yêu cầu một endpoint public sẵn sàng tiếp nhận HTTP POST với định dạng JSON và bảo mật qua Header Token.

## Research Log

### 1. Giải pháp Web Server tiếp nhận HTTP Webhook trong .NET Framework 4.5
- **Context**: Làm thế nào để mở một cổng HTTP tiếp nhận request từ bên thứ 3 trong một ứng dụng .NET 4.5 tự host mà không cần cấu hình IIS phức tạp?
- **Sources Consulted**:
  - [MSDN HttpListener Class](https://learn.microsoft.com/en-us/dotnet/api/system.net.httplistener)
  - [Microsoft ASP.NET Web API 2 Self-Host Package](https://www.nuget.org/packages/Microsoft.AspNet.WebApi.SelfHost)
- **Findings**:
  - `HttpListener` là class native có sẵn trong namespace `System.Net`, chạy trực tiếp trên Windows HTTP Server API (`http.sys`), cực kỳ gọn nhẹ và không phụ thuộc vào thư viện bên ngoài.
  - ASP.NET Web API 2 Self-Host yêu cầu cài đặt tối thiểu 5-6 package NuGet (OWIN, WebApi.Client, WebApi.Core, WebApi.SelfHost), có nguy cơ cao gây xung đột phiên bản với các project legacy khác trong solution .NET 4.5.
- **Implications**: Chọn sử dụng `HttpListener` tự viết để làm HTTP Web Server. Nó đáp ứng hoàn hảo yêu cầu đơn giản (chỉ có 1 endpoint duy nhất nhận POST JSON) và hoàn toàn native.

### 2. Xác thực và Bảo mật Webhook API
- **Context**: Đảm bảo an toàn cho endpoint public, ngăn chặn request giả mạo.
- **Sources Consulted**:
  - [RFC 6750: The OAuth 2.0 Authorization Framework: Bearer Token Usage](https://datatoolbox.org/rfc/rfc6750/)
  - [GitHub Webhooks Security & Signature Verification](https://docs.github.com/en/webhooks/using-webhooks/validating-webhook-deliveries)
- **Findings**:
  - HMAC SHA256 Signature (giống GitHub) là an toàn nhất nhưng yêu cầu hệ thống base (bên thứ ba) phải hỗ trợ tính năng ký số payload và hai bên phải chia sẻ secret.
  - Bearer Token / API Key truyền qua Header tùy chỉnh (ví dụ: `X-Webhook-Token`) là giải pháp trung hòa: bảo mật tốt trên kết nối HTTPS, cấu hình cực kỳ đơn giản và tương thích với 100% các hệ thống base hỗ trợ tùy biến header webhook.
- **Implications**: Lựa chọn giải pháp **Custom Token Header (`X-Webhook-Token`)**. Giá trị Token bí mật được cấu hình trong `App.config` của ứng dụng.

### 3. Lưu trữ chuỗi JSON tùy biến trong MySQL
- **Context**: Người dùng không biết trước payload có những gì, chỉ muốn lưu toàn bộ JSON thô để phân tích sau.
- **Sources Consulted**:
  - [MySQL Data Types - JSON vs LONGTEXT](https://dev.mysql.com/doc/refman/8.0/en/json.html)
- **Findings**:
  - Kiểu dữ liệu `JSON` được MySQL hỗ trợ từ bản 5.7.8 trở lên. Tuy nhiên, để tương thích tối đa với mọi phiên bản MySQL cũ và mới mà không lo lỗi cú pháp DB, kiểu dữ liệu `LONGTEXT` hoặc `MEDIUMTEXT` là lựa chọn an toàn nhất.
  - `LONGTEXT` trong MySQL hỗ trợ chuỗi có kích thước tối đa lên tới 4GB, hoàn toàn đáp ứng được bất kỳ payload JSON nào.
- **Implications**: Thiết kế bảng `webhook_logs` sử dụng cột `payload` với kiểu dữ liệu `LONGTEXT`.

## Architecture Pattern Evaluation

| Option | Description | Strengths | Risks / Limitations | Notes |
|--------|-------------|-----------|---------------------|-------|
| **ASP.NET Web API 2 Self-Host** | Sử dụng controller và định tuyến chuẩn REST API | Tiêu chuẩn hóa, dễ mở rộng sau này nếu có thêm nhiều API | Kéo theo rất nhiều package NuGet, làm phình to ứng dụng, có nguy cơ xung đột phiên bản | Không phù hợp cho ứng dụng siêu đơn giản 1 endpoint. |
| **HttpListener Custom Server** | Viết một vòng lặp lắng nghe HTTP thô trên luồng nền | **Cực kỳ nhẹ, 0% dependency**, kiểm soát hoàn toàn vòng đời, khởi động/dừng nhanh | Phải tự parse header, đọc body stream thủ công | **Được chọn** vì tính đơn giản, an toàn cho hệ thống legacy. |

## Design Decisions

### Decision: Chọn HttpListener làm Web Server tự host
- **Context**: Cần tiếp nhận webhook mà không muốn phụ thuộc thư viện ngoài.
- **Alternatives Considered**:
  1. ASP.NET Web API Self-Host
  2. Custom HttpListener Server
- **Selected Approach**: Custom HttpListener Server.
- **Rationale**: Hoàn toàn native trong .NET 4.5, hiệu năng cao nhờ xử lý trực tiếp HTTP stream từ Windows `http.sys`.
- **Trade-offs**: Cần tự lập trình phần định tuyến thô (routing) và kiểm tra header, nhưng vì chỉ có 1 endpoint nên code sẽ chỉ mất khoảng 50 dòng, rất sạch sẽ.

### Decision: Xử lý ghi DB bất đồng bộ bằng Task-based Asynchronous Pattern (TAP)
- **Context**: Tránh làm nghẽn luồng nhận webhook và gây timeout cho bên thứ ba.
- **Alternatives Considered**:
  1. Ghi đồng bộ trực tiếp (Synchronous)
  2. Ghi bất đồng bộ thông qua Task nền (`Task.Run`)
- **Selected Approach**: Ghi bất đồng bộ qua `Task.Run` (TPL).
- **Rationale**: Ngay sau khi đọc payload và kiểm tra token hợp lệ, Webhook Server sẽ đẩy tác vụ ghi DB vào Thread Pool thông qua `Task.Run(...)` và trả về ngay phản hồi `200 OK` cho client.
- **Trade-offs**: Cần đảm bảo quản lý vòng đời của database connection (`MySqlConnection` được khởi tạo và giải phóng đúng cách trong mỗi Task để tránh rò rỉ connection).

## Risks & Mitigations
- **Rủi ro Port Conflict (Trùng cổng)**: Cổng đăng ký (ví dụ: `8080`) bị ứng dụng khác trên server chiếm dụng.
  - *Biện pháp giảm thiểu*: Cho phép cấu hình linh hoạt Port và IP lắng nghe thông qua file `App.config` để người vận hành dễ dàng đổi cổng khi cần thiết.
- **Rủi ro Hệ thống gửi dồn dập (Spike Traffic)**: Khi hệ thống base trigger hàng ngàn event cùng lúc, dẫn tới cạn kiệt connection pool trong MySQL.
  - *Biện pháp giảm thiểu*: Giới hạn kích thước ThreadPool hoặc bắt lỗi cẩn thận, ghi log lỗi thô ra file `alarm_debug.log` nếu DB connection bị timeout.

## References
- [System.Net.HttpListener Documentation](https://learn.microsoft.com/en-us/dotnet/api/system.net.httplistener) - Hướng dẫn sử dụng class HTTP Listener native.
- [MySQL LONGTEXT Specification](https://dev.mysql.com/doc/refman/8.0/en/string-type-overview.html) - Đặc tả kiểu dữ liệu lưu chuỗi lớn trong MySQL.
