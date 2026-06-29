# Thiết kế kỹ thuật: Cập nhật Cấp độ cảnh báo AVERAGE và sửa lỗi logic cảnh báo

Tài liệu này mô tả chi tiết phương án điều chỉnh các cấp độ cảnh báo trong hệ thống nhằm hỗ trợ 3 cấp độ: `LOW` (Thấp), `AVERAGE` (Trung bình), `HIGH` (Cao) và khắc phục các lỗi logic hiện tại về cảnh báo thời gian công đoạn, cảnh báo áp suất và lỗi động cơ.

---

## 1. Các vấn đề cần giải quyết

### 1.1. Cập nhật Enum `AlarmLevel` & Giao diện hiển thị
* **Enum hiện tại**: Chỉ có `Low` và `High`.
* **Giải pháp**: Thêm `Average` vào giữa `Low` và `High` trong [AlarmLevel.cs](file:///c:/Users/tanhv/Project/HinoTools.Alarm_27092023_Test/HinoTools.Alarm_27092023_Test/HinoTools.Alarm/Model/AlarmLevel.cs).
* **Migration CSDL**: Để tránh phá vỡ dữ liệu cũ (khi `High` từng có giá trị `1` trong CSDL, nay `Average` sẽ là `1` và `High` là `2`), hệ thống sẽ thực hiện truy vấn cập nhật tự động khi khởi động `AlarmServer`:
  ```sql
  UPDATE alarmsettings SET Level = 2 WHERE Level = 1;
  ```
* **Cập nhật Giao diện hiển thị (`AlarmViewer.cs`)**:
  * Thêm xử lý màu sắc cho mức `Average` $\rightarrow$ **Màu Cam** (`Color.Orange`), chữ đen.
  * Cập nhật logic nhấp nháy (Flashing) cho mức `Average` $\rightarrow$ Nhấp nháy màu Cam/Đen.

### 1.2. Logic Cảnh báo thời gian công đoạn (`AlarmReportLogger.cs`)
* **Vấn đề hiện tại**:
  1. Chưa so sánh hiệu số tuyệt đối (chỉ so sánh hiệu số dương `deviation > 0`, dẫn đến bỏ qua lỗi chạy quá nhanh).
  2. Mức độ cảnh báo bị ngược: lệch từ $300\text{s} - 600\text{s}$ báo `ALARM`, trong khi lệch $> 600\text{s}$ lại chỉ báo `WARNING`.
* **Giải pháp**:
  1. Sử dụng `Math.Abs(actualDuration - setpoint)` để tính độ lệch tuyệt đối.
  2. Vì mỗi công đoạn chỉ có duy nhất 1 log lúc kết thúc, kích hoạt cảnh báo cho mọi độ lệch tuyệt đối $> 0$.
  3. Phân cấp mức cảnh báo theo tiêu chuẩn mới:
     * Lệch $< 300\text{s} \rightarrow$ `LOW` (Thấp - Vàng)
     * Lệch $300\text{s} - 600\text{s} \rightarrow$ `AVERAGE` (TB - Cam)
     * Lệch $> 600\text{s} \rightarrow$ `HIGH` (Cao - Đỏ)

### 1.3. Logic Cảnh báo Áp suất & Loại bỏ trùng lặp (`RealtimeThresholdLogger.cs`)
* **Vấn đề hiện tại**:
  * Cấu hình áp suất tĩnh trong `Collection` bị ngược: mức áp suất nguy hiểm hơn ($<2\text{ bar}$) lại cảnh báo `WARNING`, trong khi mức nhẹ hơn ($<3\text{ bar}$) lại cảnh báo `ALARM`.
  * Khi áp suất $< 2\text{ bar}$, cả hai điều kiện đều đúng dẫn đến việc ghi đứt quãng 2 bản ghi cùng lúc.
* **Giải pháp**:
  1. Đổi cấu hình trong `Form1.Designer.cs` của `TestData` và `WindowsFormsApp1`:
     * `AFChemTX01.ApSuat;2;<;HIGH;Áp suất hệ thống quá thấp` (Mức độ nghiêm trọng nhất)
     * `AFChemTX01.ApSuat;3;<;AVERAGE;Áp suất hệ thống quá thấp`
     *(Sử dụng chuẩn 5 phần để sinh unique alias tự động là `ApSuat_2` và `ApSuat_3` nhằm tránh xung đột)*
  2. **Cơ chế chống ghi đè/Trùng lặp theo mức nghiêm trọng (Severity Hierarchy)**:
     * Trong hàm `ScanAndLog()`, trước khi đánh giá, hệ thống sẽ xác định mức nghiêm trọng cao nhất đang bị vi phạm đối với từng `TagName`.
     * Chỉ có cấu hình có mức nghiêm trọng cao nhất bị vi phạm mới được tính là `isViolating = true`.
     * Các cấu hình có mức nghiêm trọng thấp hơn trên cùng tag sẽ bị ép về `isViolating = false`, giúp tự động phục hồi (resolve) cảnh báo cũ và chỉ giữ lại cảnh báo mức cao nhất.

### 1.4. Động cơ & Sự cố khác
* Chuyển đổi các cấu hình lỗi máy (`MayLoi` từ 1 đến 5) từ `WARNING` sang `HIGH` để tương thích với yêu cầu mức Cao (Đỏ) trong ảnh.

---

## 2. Chi tiết các tệp thay đổi

### `HinoTools.Alarm`

#### [MODIFY] [AlarmLevel.cs](file:///c:/Users/tanhv/Project/HinoTools.Alarm_27092023_Test/HinoTools.Alarm_27092023_Test/HinoTools.Alarm/Model/AlarmLevel.cs)
* Thêm phần tử `Average` vào enum.
  ```csharp
  public enum AlarmLevel
  {
      Low = 0,
      Average = 1,
      High = 2
  }
  ```

#### [MODIFY] [AlarmServer.cs](file:///c:/Users/tanhv/Project/HinoTools.Alarm_27092023_Test/HinoTools.Alarm_27092023_Test/HinoTools.Alarm/Server/AlarmServer.cs)
* Trong hàm `ActionConstructionCompleted`, chạy một câu lệnh SQL để cập nhật các bản ghi cũ từ `Level = 1` lên `Level = 2` (High):
  ```csharp
  dataAccess.ExecuteNonQuery($"UPDATE `{TableName}` SET `Level` = 2 WHERE `Level` = 1");
  ```

#### [MODIFY] [AlarmViewer.cs](file:///c:/Users/tanhv/Project/HinoTools.Alarm_27092023_Test/HinoTools.Alarm_27092023_Test/HinoTools.Alarm/Control/AlarmViewer.cs)
* Cập nhật màu nền và nhấp nháy cho `AlarmLevel.Average` thành `Color.Orange`.

---

### `HinoTools.Data`

#### [MODIFY] [AlarmReportLogger.cs](file:///c:/Users/tanhv/Project/HinoTools.Alarm_27092023_Test/HinoTools.Alarm_27092023_Test/HinoTools.Data/Log/AlarmReportLogger.cs)
* Cập nhật hàm `CheckAndLogStageDurationAlarm`:
  * Tính `deviation = Math.Abs(actualDuration - setpoint)`.
  * So sánh `deviation > 0`.
  * Phân cấp: `deviation < 300` $\rightarrow$ `LOW`, `300 <= deviation <= 600` $\rightarrow$ `AVERAGE`, `> 600` $\rightarrow$ `HIGH`.

#### [MODIFY] [RealtimeThresholdLogger.cs](file:///c:/Users/tanhv/Project/HinoTools.Alarm_27092023_Test/HinoTools.Alarm_27092023_Test/HinoTools.Data/Log/RealtimeThresholdLogger.cs)
* Thêm hàm xếp hạng mức nghiêm trọng `GetSeverityRank(string severity)`.
* Cập nhật hàm `ScanAndLog()` để lọc ra mức độ cảnh báo nghiêm trọng nhất cho mỗi `TagName` trước khi ghi nhận trạng thái vi phạm.

---

### `ConsoleApp` (Unit Tests)

#### [MODIFY] [Program.cs](file:///c:/Users/tanhv/Project/HinoTools.Alarm_27092023_Test/HinoTools.Alarm_27092023_Test/ConsoleApp/Program.cs)
* Cập nhật test case `TestStageDeviationSeverityMapping()` và `TestFivePartThresholdParsing()` phù hợp với logic phân cấp và tên cấp độ mới (`LOW`, `AVERAGE`, `HIGH`).

---

### Cập nhật Cấu hình Ứng dụng

#### [MODIFY] [TestData/Form1.Designer.cs](file:///c:/Users/tanhv/Project/HinoTools.Alarm_27092023_Test/HinoTools.Alarm_27092023_Test/TestData/Form1.Designer.cs) & [WindowsFormsApp1/Form1.Designer.cs](file:///c:/Users/tanhv/Project/HinoTools.Alarm_27092023_Test/HinoTools.Alarm_27092023_Test/WindowsFormsApp1/Form1.Designer.cs)
* Cập nhật thuộc tính `Collection` của `realtimeThresholdLogger1`:
  * Áp suất: Cấu hình dạng 5 phần với `ApSuat;2;<;HIGH` và `ApSuat;3;<;AVERAGE`.
  * Động cơ (`MayLoi`): Thay thế mức `WARNING` thành `HIGH`.
  * Các cảm biến khác: Thay thế `WARNING` thành `AVERAGE`, `ALARM` thành `HIGH`.

---

## 3. Kế hoạch xác minh (Verification Plan)

### 3.1. Kiểm thử Tự động (Automated Unit Tests)
* Chạy dự án `ConsoleApp` để đảm bảo toàn bộ các bài test logic nội bộ đều vượt qua (`SUCCESS`).

### 3.2. Kiểm thử Tích hợp (Integration Verification)
1. Biên dịch toàn bộ Solution ở chế độ `Debug`.
2. Khởi chạy `TestServer` hoặc chạy các script PowerShell kiểm tra lỗi sai lệch công đoạn.
3. Xác minh trong bảng `realtime_alarms`:
   * Khi độ lệch thời gian là $150\text{s}$ (với setpoint $100\text{s}$, thực tế $250\text{s}$ hoặc $50\text{s}$), hệ thống ghi nhận `Severity = 'LOW'`.
   * Khi độ lệch là $400\text{s}$, hệ thống ghi nhận `Severity = 'AVERAGE'`.
   * Khi độ lệch là $700\text{s}$, hệ thống ghi nhận `Severity = 'HIGH'`.
   * Khi áp suất giảm xuống $2.5\text{ bar}$, hệ thống chỉ ghi nhận 1 bản ghi `AVERAGE`. Khi áp suất giảm sâu xuống $1.5\text{ bar}$, hệ thống ghi nhận bản ghi `HIGH` và tự động đánh dấu phục hồi bản ghi `AVERAGE` trước đó.
