# Hướng dẫn Setup & Test — AlarmReportLogger, RealtimeThresholdLogger, ContinuousAlarmTag

## Mục lục

1. [Tổng quan kiến trúc test](#1-tổng-quan-kiến-trúc-test)
2. [Chọn project để test](#2-chọn-project-để-test)
3. [Setup bước 1: Kéo thả component bằng Visual Studio Designer](#3-setup-bước-1-kéo-thả-component)
4. [Setup bước 2: Cấu hình Properties trong Designer](#4-setup-bước-2-cấu-hình-properties)
5. [Setup bước 3: Cấu hình ContinuousAlarmTag trong MySQL](#5-setup-bước-3-cấu-hình-continuousalarmtag)
6. [Kịch bản test chi tiết](#6-kịch-bản-test-chi-tiết)
7. [Câu truy vấn SQL xác minh](#7-câu-truy-vấn-sql-xác-minh)
8. [Troubleshooting](#8-troubleshooting)

---

## 1. Tổng quan kiến trúc test

```
┌─────────────────────────────────────────────────────┐
│                    Form1 (WinForms)                  │
│                                                      │
│  ┌──────────┐  ┌───────────────────┐  ┌────────────┐│
│  │ iDriver1 │──│ AlarmReportLogger │  │ AlarmServer││
│  │ (SCADA)  │  │    (30s poll)     │  │(alarmlog)  ││
│  │          │──│ RealtimeThreshold │  │            ││
│  │          │  │ Logger (3s poll)  │  │            ││
│  └──────────┘  └───────────────────┘  └────────────┘│
│       │                │                     │       │
│       │                ▼                     ▼       │
│       │        ┌──────────────┐      ┌───────────┐  │
│       │        │   MySQL DB   │      │  MySQL DB  │  │
│       │        │ alarmreport  │      │  alarmlog  │  │
│       │        │ realtime_    │      └───────────┘  │
│       │        │ alarms       │                      │
│       │        └──────────────┘                      │
└───────┼──────────────────────────────────────────────┘
        │
   SCADA PLC (AFChemPLC)
```

**3 tính năng cần test:**

| # | Tính năng | Component | Bảng DB | Tần suất |
|---|-----------|-----------|---------|----------|
| 1 | Thống kê mẻ trộn | `AlarmReportLogger` | `alarmreport` | 30s |
| 2 | Cảnh báo vượt ngưỡng | `RealtimeThresholdLogger` | `realtime_alarms` | 3s |
| 3 | Khắc phục log rác | `ContinuousAlarmTag` (trong `AlarmServer`) | `alarmlog` | Event-driven |

---

## 2. Chọn project để test

### Project `TestData` ✅ (Khuyến nghị)
- Đã reference **cả hai** `HinoTools.Data` và `HinoTools.Alarm`
- Form1 đang trống, sẵn sàng để thêm component
- Phù hợp để test cả 3 tính năng

### Project `TestServer` (Backup)
- Đã có AlarmServer, AlarmLogger, EventServer... sẵn
- Nhưng **chưa reference** `HinoTools.Data` → cần thêm reference trước khi kéo thả `AlarmReportLogger` / `RealtimeThresholdLogger`

**→ Dùng `TestData` cho đơn giản.**

---

## 3. Setup bước 1: Kéo thả component

### Bước 3.1: Build Solution trước
1. Mở Solution `HinoTools.Alarm.sln` trong Visual Studio 2019
2. Nhấn `Ctrl + Shift + B` (Build Solution)
3. Đảm bảo build thành công — 0 errors

### Bước 3.2: Mở Form1 ở chế độ Designer
1. Trong Solution Explorer → `TestData` → double-click `Form1.cs`
2. Form1 sẽ mở ở chế độ **Designer** (giao diện kéo thả)

### Bước 3.3: Tìm component trong Toolbox
1. Mở **Toolbox** (View → Toolbox hoặc `Ctrl+Alt+X`)
2. Tìm section **"HinoTools.Data.Log Components"** (hoặc General)
3. Nếu **KHÔNG thấy** component → xem [Troubleshooting #8.1](#81-không-thấy-component-trong-toolbox)

### Bước 3.4: Kéo thả vào Form1

**Kéo thả theo thứ tự:**

```
1. iDriver         (từ ATSCADA section — BẮT BUỘC, làm đầu tiên)
2. AlarmReportLogger     (từ HinoTools.Data.Log)
3. RealtimeThresholdLogger (từ HinoTools.Data.Log)
4. AlarmServer      (từ HinoTools.Alarm.Server — cho test ContinuousAlarmTag)
5. AlarmLogger      (từ HinoTools.Alarm.Control — ghi log vào alarmlog)
```

Sau khi kéo thả, bạn sẽ thấy các component xuất hiện ở **Component Tray** (phía dưới Form).

---

## 4. Setup bước 2: Cấu hình Properties trong Designer

Chọn từng component ở Component Tray → mở **Properties Window** (`F4`) → cấu hình:

### 4.1. iDriver1
| Property | Giá trị |
|----------|---------|
| `ProjectFile` | *(đường dẫn đến file .atp của project ATSCADA, ví dụ: `C:\SCADA\AFChem.atp`)* |

### 4.2. AlarmReportLogger (alarmReportLogger1)
| Property | Giá trị | Ghi chú |
|----------|---------|---------|
| `Driver` | `iDriver1` | ← chọn từ dropdown |
| `ServerName` | `localhost` | |
| `UserID` | `root` | |
| `Password` | `101101` | ← kiểm tra password MySQL của bạn |
| `DatabaseName` | `scada` | |
| `TableName` | `alarmreport` | |
| `PollingInterval` | `30000` | 30 giây (mặc định) |
| `Collection` | *(xem bảng bên dưới)* | Nhấn `...` để mở String Collection Editor |

**Collection** — Nhấn nút `...` bên cạnh property → String Collection Editor → nhập mỗi dòng:

```
AFChemPLC.QuyTrinh;QuyTrinh
AFChemPLC.CongDoanMay;CongDoanMay
AFChemPLC.ThoiGianCapLieu;ThoiGianCapLieu
AFChemPLC.ThoiGianTron1;ThoiGianTron1
AFChemPLC.ThoiGianXaDay;ThoiGianXaDay
AFChemPLC.ThoiGianHutXaDay;ThoiGianHutXaDay
AFChemPLC.ThoiGianTron2;ThoiGianTron2
AFChemPLC.ThoiGianRungXaDay;ThoiGianRungXaDay
AFChemPLC.ThoiGianXaHang;ThoiGianXaHang
AFChemPLC.ThoiGianRungXaHang;ThoiGianRungXaHang
AFChemPLC.ApSuat;ApSuat
AFChemPLC.NhietDoMoiTruong;NhietDoMoiTruong
AFChemPLC.DoAmMoiTruong;DoAmMoiTruong
AFChemPLC.NhietDoBonTronTren;NhietDoBonTronTren
AFChemPLC.NhietDoBonTronGiua;NhietDoBonTronGiua
AFChemPLC.NhietDoBonTronDuoi;NhietDoBonTronDuoi
```

> **Lưu ý:** Cần thay `AFChemPLC` bằng tên Device thực tế trong project ATSCADA của bạn. Các tên tag (sau dấu `.`) cũng phải khớp chính xác với tag đã cấu hình trong ATSCADA.

### 4.3. RealtimeThresholdLogger (realtimeThresholdLogger1)
| Property | Giá trị | Ghi chú |
|----------|---------|---------|
| `Driver` | `iDriver1` | ← chọn từ dropdown |
| `ServerName` | `localhost` | |
| `UserID` | `root` | |
| `Password` | `101101` | |
| `DatabaseName` | `scada` | |
| `TableName` | `realtime_alarms` | |
| `ScanInterval` | `3000` | 3 giây (mặc định) |
| `Collection` | *(xem bảng bên dưới)* | |

**Collection** — Nhấn `...` → nhập mỗi dòng:

```
AFChemPLC.NhietDoBonTronTren;NhietDoBonTronTren;50;>
AFChemPLC.ApSuat;ApSuat;10;<
```

**Format:** `TagName;Alias;Threshold;Operator`

| Ví dụ | Ý nghĩa |
|-------|---------|
| `AFChemPLC.NhietDoBonTronTren;NhietDoBonTronTren;50;>` | Cảnh báo khi Nhiệt độ bồn trộn trên **> 50** |
| `AFChemPLC.ApSuat;ApSuat;10;<` | Cảnh báo khi Áp suất **< 10** |

### 4.4. AlarmServer (alarmServer1) — Cho test ContinuousAlarmTag
| Property | Giá trị |
|----------|---------|
| `Driver` | `iDriver1` |
| `ServerName` | `localhost` |
| `UserID` | `root` |
| `Password` | `101101` |
| `DatabaseName` | `scada` |
| `TableName` | `alarmsettings` |
| `TableLog` | `alarmlog` |
| `Limit` | `200` |

### 4.5. AlarmLogger (alarmLogger1)
| Property | Giá trị |
|----------|---------|
| `AlarmHub` | `alarmServer1` |
| `ServerName` | `localhost` |
| `UserID` | `root` |
| `Password` | `101101` |
| `DatabaseName` | `scada` |
| `TableName` | `alarmlog` |

---

## 5. Setup bước 3: Cấu hình ContinuousAlarmTag trong MySQL

`ContinuousAlarmTag` được kích hoạt qua cấu hình trong bảng `alarmsettings`. Mở MySQL Workbench hoặc HeidiSQL và chạy:

```sql
-- Thêm thanh ghi timer vào alarmsettings với Type = 2 (Continuous)
INSERT INTO scada.alarmsettings 
  (TagName, TagNo, Value, Location, Description, Type, Level, FaultCode)
VALUES 
  ('AFChemPLC.ThoiGianCapLieu', 'T001', '0', 'Mixer', 'Timer Cấp Liệu', 2, 0, 0),
  ('AFChemPLC.ThoiGianTron1', 'T002', '0', 'Mixer', 'Timer Trộn 1', 2, 0, 0),
  ('AFChemPLC.ThoiGianRungXaHang', 'T008', '0', 'Mixer', 'Timer Rung Xả Hàng', 2, 0, 0);
```

**Giải thích cột `Type`:**

| Giá trị | AlarmType | Mô tả |
|---------|-----------|-------|
| `0` | `Bit` | Kiểm tra bit trong word (alarm rời rạc) |
| `1` | `Value` | So sánh string chính xác (alarm rời rạc) |
| **`2`** | **`Continuous`** | **Timer liên tục: chỉ ghi 1 lần khi 0→>0, update RestoreTime khi >0→0** |

---

## 6. Kịch bản test chi tiết

### Test 1: AlarmReportLogger — Mẻ trộn 30s

```
Bước 1: Chạy TestData → Form1 hiển thị
Bước 2: Trong ATSCADA (hoặc PLC), set ThoiGianCapLieu = 5
        ✅ Kỳ vọng: State Machine chuyển sang Logging, QuyTrinh = 1
Bước 3: Đợi 60 giây
        ✅ Kỳ vọng: Có 2-3 bản ghi trong bảng alarmreport
Bước 4: Set ThoiGianRungXaHang = 100, đợi 30s
        ✅ Kỳ vọng: hasRungXaHangStarted = true, vẫn tiếp tục ghi log
Bước 5: Set ThoiGianRungXaHang = 0
        ✅ Kỳ vọng: Ghi 1 bản ghi cuối → chuyển về Idle → NGỪNG ghi
Bước 6: Đợi 60 giây
        ✅ Kỳ vọng: Không có bản ghi mới nào
Bước 7: Set ThoiGianCapLieu = 3
        ✅ Kỳ vọng: Mẻ mới, QuyTrinh = 2
```

### Test 2: RealtimeThresholdLogger — Cảnh báo vượt ngưỡng

```
Bước 1: Set NhietDoBonTronTren = 30 (< 50)
        ✅ Kỳ vọng: Không INSERT
Bước 2: Set NhietDoBonTronTren = 55 (> 50)
        ✅ Kỳ vọng: INSERT 1 bản ghi vào realtime_alarms
Bước 3: Giữ NhietDoBonTronTren = 55, đợi 30 giây
        ✅ Kỳ vọng: KHÔNG INSERT thêm (debounce hoạt động)
Bước 4: Set NhietDoBonTronTren = 40 (< 50)
        ✅ Kỳ vọng: Reset alarm state
Bước 5: Set NhietDoBonTronTren = 60
        ✅ Kỳ vọng: INSERT 1 bản ghi mới
```

### Test 3: ContinuousAlarmTag — Khắc phục log rác

```
Bước 1: Đảm bảo alarmsettings có dòng Type=2 (xem Bước 5)
Bước 2: Set ThoiGianCapLieu = 0 (trạng thái ban đầu)
        ✅ Kỳ vọng: Không INSERT vào alarmlog
Bước 3: Set ThoiGianCapLieu = 1 (0 → > 0)
        ✅ Kỳ vọng: INSERT 1 bản ghi: Status=Alarm, OccurrenceTime = now
Bước 4: Set ThoiGianCapLieu = 5 (vẫn > 0)
        ✅ Kỳ vọng: KHÔNG INSERT thêm (đây là điểm khác biệt với ValueAlarmTag!)
Bước 5: Set ThoiGianCapLieu = 100 (vẫn > 0)
        ✅ Kỳ vọng: KHÔNG INSERT thêm
Bước 6: Set ThoiGianCapLieu = 0 (> 0 → 0)
        ✅ Kỳ vọng: UPDATE bản ghi trước đó: Status=Normal, RestoreTime = now
Bước 7: Kiểm tra alarmlog
        ✅ Kỳ vọng: Chỉ có 1 bản ghi cho chuỗi 0→1→5→100→0
```

---

## 7. Câu truy vấn SQL xác minh

### 7.1. Kiểm tra bảng `alarmreport`
```sql
-- Xem dữ liệu mới nhất
SELECT * FROM scada.alarmreport ORDER BY ID DESC LIMIT 20;

-- Xem theo QuyTrinh
SELECT QuyTrinh, COUNT(*) as Records, 
       MIN(DateTime) as Start, MAX(DateTime) as End
FROM scada.alarmreport 
GROUP BY QuyTrinh ORDER BY QuyTrinh DESC;

-- Kiểm tra khoảng cách 30s
SELECT a.ID, a.DateTime, 
       TIMESTAMPDIFF(SECOND, b.DateTime, a.DateTime) as Gap_Seconds
FROM scada.alarmreport a
JOIN scada.alarmreport b ON a.ID = b.ID + 1 
  AND a.QuyTrinh = b.QuyTrinh
ORDER BY a.ID DESC LIMIT 10;
```

### 7.2. Kiểm tra bảng `realtime_alarms`
```sql
-- Xem cảnh báo mới nhất
SELECT * FROM scada.realtime_alarms ORDER BY ID DESC LIMIT 20;

-- Đếm theo tag (debounce check)
SELECT TagName, COUNT(*) as AlertCount,
       MIN(DateTime) as First, MAX(DateTime) as Last
FROM scada.realtime_alarms GROUP BY TagName;
```

### 7.3. Kiểm tra bảng `alarmlog` (ContinuousAlarmTag)
```sql
-- Xem alarm log cho timer registers
SELECT ID, TagName, Status, OccurrenceTime, RestoreTime 
FROM scada.alarmlog 
WHERE TagName LIKE '%ThoiGian%'
ORDER BY OccurrenceTime DESC LIMIT 10;

-- Đếm: phải chỉ có 1 bản ghi/chu kỳ
SELECT TagName, COUNT(*) as Records
FROM scada.alarmlog 
WHERE TagName LIKE '%ThoiGian%'
  AND OccurrenceTime >= DATE_SUB(NOW(), INTERVAL 10 MINUTE)
GROUP BY TagName;
```

---

## 8. Troubleshooting

### 8.1. Không thấy component trong Toolbox

**Nguyên nhân:** Visual Studio chưa load component từ DLL mới build.

**Cách fix:**
1. **Build lại Solution** (`Ctrl + Shift + B`)
2. Đóng Form Designer → mở lại
3. Nếu vẫn không thấy → Right-click Toolbox → **"Choose Items..."** → Tab **.NET Framework Components** → Browse → chọn:
   - `HinoTools.Data\bin\Debug\HinoTools.Data.dll`
4. Tick chọn `AlarmReportLogger` và `RealtimeThresholdLogger` → OK

### 8.2. Lỗi "Could not find type" khi mở Designer

**Nguyên nhân:** DLL chưa được build hoặc reference thiếu.

**Cách fix:**
1. Build Solution trước
2. Kiểm tra `TestData.csproj` đã reference `HinoTools.Data`:
   ```xml
   <ProjectReference Include="..\HinoTools.Data\HinoTools.Data.csproj">
     <Project>{81FD1EFB-E3D6-4B67-A461-8C17B14466EA}</Project>
     <Name>HinoTools.Data</Name>
   </ProjectReference>
   ```

### 8.3. Không thấy dropdown `iDriver1` trong property Driver

**Nguyên nhân:** `iDriver1` chưa được kéo thả vào Form.

**Cách fix:** Kéo thả `iDriver` từ Toolbox vào Form1 **TRƯỚC** khi cấu hình AlarmReportLogger.

### 8.4. Bảng không tự tạo trong MySQL

**Nguyên nhân:** Lỗi kết nối MySQL hoặc sai password.

**Cách fix:**
1. Kiểm tra MySQL đang chạy: `net start mysql`
2. Kiểm tra password đúng trong property component
3. Thử tạo thủ công: `CREATE DATABASE IF NOT EXISTS scada;`

### 8.5. AlarmReportLogger không ghi log dù ThoiGianCapLieu > 0

**Checklist:**
- [ ] `iDriver1.ProjectFile` đã set đúng đường dẫn `.atp`
- [ ] Property `Driver` = `iDriver1` (không phải null)
- [ ] Collection đã nhập đúng format `TagName;Alias`
- [ ] Tag name trong Collection khớp với ATSCADA project
- [ ] MySQL đang chạy và password đúng

### 8.6. ContinuousAlarmTag vẫn spam log

**Kiểm tra:**
1. Xem cột `Type` trong `alarmsettings` — phải = `2` (không phải `1`)
2. Query:
   ```sql
   SELECT TagName, Type FROM scada.alarmsettings 
   WHERE TagName LIKE '%ThoiGian%';
   ```
3. Nếu Type = 1 → UPDATE thành 2:
   ```sql
   UPDATE scada.alarmsettings SET Type = 2 
   WHERE TagName LIKE '%ThoiGian%';
   ```
4. Restart ứng dụng sau khi đổi Type (AlarmServer chỉ đọc config lúc khởi động)
