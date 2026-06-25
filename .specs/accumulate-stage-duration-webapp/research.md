# Research & Design Decisions - WebApp Accumulate Stage Duration

---
**Purpose**: Capture discovery findings, architectural investigations, and rationale that inform the technical design of the WebApp Accumulator.
---

## Summary
- **Feature**: `accumulate-stage-duration-webapp`
- **Discovery Scope**: Extension
- **Key Findings**:
  - WebApp hiện đang hiển thị trực tiếp dữ liệu thô của 8 thanh ghi thời gian từ WCF tags lên UI, dẫn đến hiện tượng nhảy về 0s khi máy tạm dừng (PLC reset thanh ghi).
  - Backend đã có API `GetCurrentBatchStats` thực hiện lấy dữ liệu từ bảng `alarmreport` của `runId` hoạt động, nhưng chưa lấy giá trị lớn nhất (MAX) của 8 công đoạn.
  - C# Logger đã triển khai cơ chế tích lũy phần mềm (Software Accumulator) lưu vào DB, do đó WebApp chỉ cần kế thừa cách tính này trên Client và đồng bộ hóa thông tin từ DB để khắc phục hoàn toàn sự cố.

## Research Log

### 1. Backend Database Query for Accumulated stage durations
- **Context**: Làm thế nào để lấy các giá trị tích lũy thực tế từ DB khi tải lại trang hoặc chạy định kỳ?
- **Sources Consulted**: Cấu trúc bảng `alarmreport` của Logger C# và schema DB MySQL của WebApp.
- **Findings**:
  - Bảng `alarmreport` có 8 cột dạng VARCHAR(200): `ThoiGianCapLieu`, `ThoiGianTron1`, `ThoiGianXaDay`, `ThoiGianRungXaDay`, `ThoiGianHutXaDay`, `ThoiGianTron2`, `ThoiGianXaHang`, `ThoiGianRungXaHang`.
  - Giá trị tích lũy lớn nhất của mỗi công đoạn cho mẻ hiện tại (`runId`) được lấy bằng hàm: `MAX(CAST(col AS DECIMAL(10,2)))`.
- **Implications**: Cập nhật hàm C# `GetCurrentBatchStats` trong `OverviewController.cs` để query dữ liệu này và trả về trong đối tượng JSON `batchInfo.accumulatedValues`.

### 2. Client-side Realtime Tag Synchronization
- **Context**: Tránh lỗi lệch số giữa Client và Database do độ trễ mạng hoặc tải trang (F5).
- **Sources Consulted**: Logic `UpdateAccumulator` của C# Logger và cơ chế `updateTag` WCF của WebApp.
- **Findings**:
  - Cần chạy song song bộ tích lũy Client-side JS Accumulator. Khi tag WCF thay đổi giá trị, JS sẽ tính toán delta và cộng dồn vào bộ đếm.
  - Khi API polling (30s) trả về kết quả, WebApp Client sẽ thực hiện đồng bộ hóa bộ đếm JS bằng cách so sánh: `jsAccumulatedTimers[code] = Math.max(jsAccumulatedTimers[code], dbValue)`.
- **Implications**: Giải pháp này tạo cơ chế Self-healing (Tự phục hồi) giúp giao diện luôn đúng ngay cả khi người dùng F5 hoặc có nhiễu kết nối tạm thời.

## Architecture Pattern Evaluation

| Option | Description | Strengths | Risks / Limitations | Notes |
|--------|-------------|-----------|---------------------|-------|
| **Option A: Pure DB Polling** | Chỉ hiển thị dữ liệu từ DB (query mỗi 1-2 giây) | Không cần viết logic tích lũy ở client. | Gây tải cực kỳ lớn cho MySQL Server khi nhiều client truy cập; không đáp ứng được tần suất cập nhật thực sự realtime (<1s). | Bị loại do vấn đề hiệu năng. |
| **Option B: Hybrid Client-Server Accumulator** | Tích lũy realtime tại Client bằng JS; Đồng bộ định kỳ 30s với Database. | Tiết kiệm băng thông, giảm tải DB, hiển thị mượt mà realtime chu kỳ 1s. | Phức tạp hơn ở phần đồng bộ hóa Client-Server. | **Được chọn**. Khắc phục tốt nhất cả về UX và Performance. |

## Design Decisions

### Decision: Hybrid Client-Server Accumulator
- **Context**: Cần cơ chế cập nhật realtime mượt mà nhưng vẫn chính xác theo dữ liệu lịch sử trong DB.
- **Selected Approach**: Triển khai bộ tích lũy JS Accumulator trên giao diện, hạt giống (seed) ban đầu và đồng bộ hóa định kỳ được nạp từ DB MySQL thông qua API C#.
- **Rationale**: Đảm bảo trải nghiệm người dùng tối ưu, dữ liệu cập nhật ngay lập tức theo chu kỳ tag WCF thay đổi (1s) nhưng không bị trôi số sau thời gian dài nhờ có DB làm mốc hiệu chuẩn (calibration).
- **Trade-offs**: Cần thêm mã nguồn JS quản lý trạng thái bộ tích lũy trên trình duyệt.

## Risks & Mitigations
- **Lệch giây giữa client và DB:** Được khắc phục bằng cách so sánh và lấy giá trị lớn hơn giữa Client và DB mỗi 30s.
- **Chuyển đổi mẻ mới bị giữ số cũ:** Được khắc phục bằng cách phát hiện sự thay đổi `runId` trong API Response để tự động reset bộ tích lũy về 0.

## References
- [C# Software Accumulator Logic](file:///c:/Users/tanhv/Project/HinoTools.Alarm_27092023_Test/HinoTools.Alarm_27092023_Test/HinoTools.Data/Log/AlarmReportLogger.cs#L757-L790) — Source of truth cho thuật toán tích lũy.
