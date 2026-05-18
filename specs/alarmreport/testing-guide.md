# Hướng dẫn Kiểm thử (Testing Guide) - Module Alarm & Report
*Giai đoạn 4 — Manual Integration Testing*

> [!IMPORTANT]
> Các bài test dưới đây yêu cầu kết nối **MySQL Server** và **SCADA Driver (iDriver)** đang chạy.
> Thực hiện trên máy có cài đặt ATSCADA + MySQL.

---

## Chuẩn bị

### 1. Cấu hình MySQL
- Server: `localhost`, User: `root`, Password: `100100` (hoặc `101101`)
- Database: `scada` (sẽ được tự động tạo)

### 2. Cấu hình SCADA
- Mở project ATSCADA có Device `AFChemPLC` với đầy đủ 17 tag thanh ghi.
- Đảm bảo `iDriver` có thể đọc giá trị từ PLC.

---

## Task 4.1: Giả lập dữ liệu tag

### Cách 1: Dùng ATSCADA Simulator
1. Mở ATSCADA Project → đặt chế độ Simulation cho Device `AFChemPLC`.
2. Dùng Tag Editor để set giá trị trực tiếp cho các thanh ghi.

### Cách 2: Dùng TestServer (WinForms)
Thêm component `AlarmReportLogger` và `RealtimeThresholdLogger` vào `Form1.Designer.cs`:

```csharp
// Trong Form1.Designer.cs - InitializeComponent():

// === AlarmReportLogger ===
this.alarmReportLogger1 = new HinoTools.Data.Log.AlarmReportLogger(this.components);
this.alarmReportLogger1.Driver = this.iDriver1;
this.alarmReportLogger1.ServerName = "localhost";
this.alarmReportLogger1.UserID = "root";
this.alarmReportLogger1.Password = "101101";
this.alarmReportLogger1.DatabaseName = "scada";
this.alarmReportLogger1.TableName = "alarmreport";
this.alarmReportLogger1.PollingInterval = 30000;
this.alarmReportLogger1.Collection = new string[] {
    "AFChemPLC.ThoiGianCapLieu;ThoiGianCapLieu",
    "AFChemPLC.ThoiGianTron1;ThoiGianTron1",
    "AFChemPLC.ThoiGianXaDay;ThoiGianXaDay",
    "AFChemPLC.ThoiGianRungXaDay;ThoiGianRungXaDay",
    "AFChemPLC.ThoiGianHutXaDay;ThoiGianHutXaDay",
    "AFChemPLC.ThoiGianTron2;ThoiGianTron2",
    "AFChemPLC.ThoiGianXaHang;ThoiGianXaHang",
    "AFChemPLC.ThoiGianRungXaHang;ThoiGianRungXaHang",
    "AFChemPLC.NhietDoMay;NhietDoMay",
    // ... thêm các tag còn lại
    "AFChemPLC.CongDoanMay;CongDoanMay"
};

// === RealtimeThresholdLogger ===
this.realtimeThresholdLogger1 = new HinoTools.Data.Log.RealtimeThresholdLogger(this.components);
this.realtimeThresholdLogger1.Driver = this.iDriver1;
this.realtimeThresholdLogger1.ServerName = "localhost";
this.realtimeThresholdLogger1.UserID = "root";
this.realtimeThresholdLogger1.Password = "101101";
this.realtimeThresholdLogger1.DatabaseName = "scada";
this.realtimeThresholdLogger1.TableName = "realtime_alarms";
this.realtimeThresholdLogger1.ScanInterval = 3000;
this.realtimeThresholdLogger1.Collection = new string[] {
    "AFChemPLC.NhietDoMay;NhietDoMay;50;>",
    "AFChemPLC.ApSuat;ApSuat;10;<"
};
```

Khai báo field trong Form1.Designer.cs:
```csharp
private HinoTools.Data.Log.AlarmReportLogger alarmReportLogger1;
private HinoTools.Data.Log.RealtimeThresholdLogger realtimeThresholdLogger1;
```

> [!NOTE]
> Cần thêm project reference `HinoTools.Data` vào `TestServer.csproj`.

---

## Task 4.2: Xác minh bảng `alarmreport`

### Kịch bản test

| Bước | Hành động | Kết quả mong đợi |
|------|-----------|-------------------|
| 1 | Set `ThoiGianCapLieu` = 5 (> 0) | State Machine chuyển sang `Logging`, QuyTrinh = MAX+1 |
| 2 | Đợi 60 giây | Ít nhất 2 bản ghi INSERT vào `alarmreport` |
| 3 | Set `CongDoanMay` = 2, `ThoiGianTron1` = 10 | Bản ghi tiếp theo có CongDoanMay = 2 |
| 4 | Set `ThoiGianRungXaHang` = 100 | `hasRungXaHangStarted` = true |
| 5 | Set `ThoiGianRungXaHang` = 0 | State Machine chuyển về `Idle`, ngừng ghi log |
| 6 | Đợi 60 giây | Không có bản ghi mới nào |
| 7 | Set `ThoiGianCapLieu` = 3 | Mẻ mới bắt đầu, QuyTrinh tăng thêm 1 |

### Câu truy vấn kiểm tra
```sql
-- Xem toàn bộ dữ liệu
SELECT * FROM alarmreport ORDER BY ID DESC LIMIT 20;

-- Kiểm tra QuyTrinh tăng đúng
SELECT QuyTrinh, COUNT(*) as SoLuongBanGhi, 
       MIN(DateTime) as BatDau, MAX(DateTime) as KetThuc
FROM alarmreport 
GROUP BY QuyTrinh 
ORDER BY QuyTrinh DESC;

-- Kiểm tra khoảng cách 30s giữa các bản ghi
SELECT a.ID, a.DateTime, 
       TIMESTAMPDIFF(SECOND, b.DateTime, a.DateTime) as KhoangCach_Giay
FROM alarmreport a
JOIN alarmreport b ON a.ID = b.ID + 1 AND a.QuyTrinh = b.QuyTrinh
WHERE a.QuyTrinh = (SELECT MAX(QuyTrinh) FROM alarmreport)
ORDER BY a.ID;
```

---

## Task 4.3: Xác minh bảng `realtime_alarms`

### Kịch bản test

| Bước | Hành động | Kết quả mong đợi |
|------|-----------|-------------------|
| 1 | Set `NhietDoMay` = 30 (< ngưỡng 50) | Không có INSERT |
| 2 | Set `NhietDoMay` = 55 (> ngưỡng 50) | INSERT 1 bản ghi |
| 3 | Giữ `NhietDoMay` = 55, đợi 30 giây | **KHÔNG INSERT thêm** (debounce) |
| 4 | Set `NhietDoMay` = 40 (< 50) | Reset trạng thái alarm |
| 5 | Set `NhietDoMay` = 60 | INSERT 1 bản ghi mới (lần vượt ngưỡng mới) |

### Câu truy vấn kiểm tra
```sql
-- Xem cảnh báo mới nhất
SELECT * FROM realtime_alarms ORDER BY ID DESC LIMIT 20;

-- Kiểm tra debounce: không nên có INSERT liên tiếp cùng TagName trong vài giây
SELECT TagName, COUNT(*) as SoLanCanhBao,
       MIN(DateTime) as LanDau, MAX(DateTime) as LanCuoi
FROM realtime_alarms 
GROUP BY TagName;
```

---

## Task 4.4: Xác minh bảng `alarmlog` (ContinuousAlarmTag)

### Điều kiện
- Trong bảng `alarmsettings`, thêm 1 dòng cấu hình với `Type = 2` (Continuous):

```sql
INSERT INTO alarmsettings (TagName, TagNo, Value, Location, Description, Type, Level, FaultCode)
VALUES ('AFChemPLC.ThoiGianCapLieu', 'T001', '0', 'Mixer', 'Timer Cap Lieu', 2, 0, 0);
```

### Kịch bản test

| Bước | Hành động | Kết quả mong đợi |
|------|-----------|-------------------|
| 1 | Set `ThoiGianCapLieu` = 0 | Status = NORMAL, không INSERT |
| 2 | Set `ThoiGianCapLieu` = 1 (0 → > 0) | INSERT 1 bản ghi: Status=Alarm, OccurrenceTime = now |
| 3 | Set `ThoiGianCapLieu` = 5 (vẫn > 0) | **KHÔNG INSERT thêm** (giữ nguyên Alarm) |
| 4 | Set `ThoiGianCapLieu` = 100 (vẫn > 0) | **KHÔNG INSERT thêm** |
| 5 | Set `ThoiGianCapLieu` = 0 (> 0 → 0) | UPDATE bản ghi: Status=Normal, RestoreTime = now |
| 6 | Kiểm tra bảng alarmlog | Chỉ có **1 bản ghi duy nhất** cho cả chuỗi 0→1→5→100→0 |

### Câu truy vấn kiểm tra
```sql
-- Xem alarm log mới nhất
SELECT ID, TagName, TagNo, Status, OccurrenceTime, RestoreTime 
FROM alarmlog 
WHERE TagName = 'AFChemPLC.ThoiGianCapLieu'
ORDER BY OccurrenceTime DESC 
LIMIT 10;

-- Đếm số bản ghi: phải = 1 cho mỗi chu kỳ
SELECT COUNT(*) as SoBanGhi
FROM alarmlog 
WHERE TagName = 'AFChemPLC.ThoiGianCapLieu'
  AND OccurrenceTime >= DATE_SUB(NOW(), INTERVAL 10 MINUTE);
```

---

## Checklist tóm tắt

- [ ] Toàn bộ solution build thành công ✅ (đã xác minh)
- [ ] `alarmreport`: Ghi đúng 30s/lần, bắt đầu/kết thúc đúng mẻ
- [ ] `alarmreport`: QuyTrinh tự động tăng khi mẻ mới
- [ ] `realtime_alarms`: INSERT tức thời khi vượt ngưỡng
- [ ] `realtime_alarms`: Debounce hoạt động (không INSERT liên tục)
- [ ] `alarmlog`: ContinuousAlarmTag chỉ sinh 1 bản ghi cho cả quá trình
