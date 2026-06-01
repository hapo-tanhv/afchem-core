# Thiết kế Kỹ thuật Nâng cấp: Module Quản lý Batch (Lô) - Run (Mẻ) & Tích hợp HTTP API

Tài liệu này mô tả chi tiết thiết kế kỹ thuật nâng cấp để hỗ trợ bài toán: **1 Batch (Lô sản xuất) có thể bao gồm nhiều Mẻ sản xuất (Run)**, mỗi mẻ luôn có **8 công đoạn** hoạt động liên tục. Thiết kế này đảm bảo khả năng tương thích ngược hoàn toàn với cơ sở dữ liệu và dữ liệu lịch sử hiện có.

---

## 1. Kiến trúc Hệ thống và Sơ đồ Luồng dữ liệu (Architecture & Data Flow)

Hệ thống được nâng cấp để quản lý vòng đời độc lập của từng Lô (`batches`) và Mẻ (`runs`), đồng thời liên kết chính xác các dữ liệu giám sát SCADA.

```
                               ┌──────────────────────────────────────────────┐
                               │             ỨNG DỤNG BÊN THỨ BA / MES        │
                               └──────────────────────┬───────────────────────┘
                                                      │ HTTP API (Port 5500)
                                                      ▼
┌──────────────────────────────────────────────────────────────────────────────────────────────────┐
│                                     HinoTools.Alarm Server                                       │
│                                                                                                  │
│  ┌─────────────────────────┐                            ┌─────────────────────────────────────┐  │
│  │    BatchesHttpServer    │                            │          AlarmReportLogger          │  │
│  │ (POST /api/batches/cre  │                            │    (Giám sát 8 công đoạn của PLC)   │  │
│  │  GET /api/batches, runs)│                            └──────────────────┬──────────────────┘  │
│  └────────────┬────────────┘                                               │                     │
│               │                                                            │                     │
│               │ Tạo Lô & Mẻ con (Pending)                                  │ Kích hoạt & Ghi log │
│               ▼                                                            ▼                     │
│  ┌────────────────────────────────────────────────────────────────────────────────────────────┐  │
│  │                                           MySQL DB                                         │  │
│  │                                                                                            │  │
│  │    ┌───────────────┐               ┌───────────────┐              ┌─────────────────────┐  │  │
│  │    │    batches    │ ◄───────────  │     runs      │ ◄─────────── │     alarmreport     │  │  │
│  │    │  (Lô sản xuất)│ 1           N │ (Mẻ sản xuất) │ 1          N │   (batchId, runId)  │  │  │
│  │    └───────────────┘               └───────────────┘              └─────────────────────┘  │  │
│  │                                                                                               │  │
│  │                                                                   ┌─────────────────────┐  │  │
│  │                                                                   │      alarmlog       │  │  │
│  │                                                                   │   (batchId, runId)  │  │  │
│  │                                                                   └─────────────────────┘  │  │
│  └────────────────────────────────────────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────────────────────────────────────┘
```

### Chi tiết luồng nghiệp vụ mới:
1. **Khai báo Lô/Mẻ**: Bên thứ ba tạo Lô mới thông qua API (`POST /api/batches/create` kèm `runs_count`). Hệ thống sẽ tạo 1 dòng trong `batches` và `runs_count` dòng trong `runs` ở trạng thái `Pending`.
2. **Kích hoạt Mẻ (FIFO)**:
   - Khi SCADA phát hiện `ThoiGianCapLieu > 0` lúc đang ở `Idle`:
   - Hệ thống quét cơ sở dữ liệu tìm mẻ `Pending` cũ nhất của thiết bị đó.
   - Cập nhật trạng thái mẻ con đó thành `Active`.
   - Nếu Lô cha đang ở trạng thái `Pending`, cập nhật trạng thái Lô cha thành `Active`.
   - Lưu `activeRunId` và `activeBatchId` vào bộ nhớ để ghi log.
3. **Ghi log liên kết**:
   - Trong quá trình chạy 8 công đoạn, tất cả dữ liệu chèn vào `alarmreport` và `realtime_alarms` sẽ được liên kết trực tiếp bằng cả hai khóa ngoại `batchId` và `runId`.
4. **Kết thúc Mẻ & Đóng Lô**:
   - Khi kết thúc công đoạn 8 (Xả hàng hoàn tất):
   - Cập nhật trạng thái mẻ con (`runs`) đó thành `Completed`.
   - Quét DB kiểm tra xem tất cả các mẻ con khác của Lô đó đã `Completed` hết chưa.
     - **Nếu rồi**: Cập nhật Lô (`batches`) thành `Completed`.
     - **Nếu chưa**: Giữ nguyên Lô là `Active` để chờ mẻ tiếp theo (ví dụ: mẻ tiếp theo chạy vào buổi chiều).

---

## 2. Thiết kế Cơ sở dữ liệu chi tiết (Database Schema Design)

### 2.1. Cấu trúc bảng `runs` (Bảng mới)
```sql
CREATE TABLE IF NOT EXISTS `runs` (
  `id` INT AUTO_INCREMENT PRIMARY KEY,
  `batch_id` INT NOT NULL,
  `run_number` INT NOT NULL,
  `name` VARCHAR(150) NOT NULL UNIQUE,
  `status` VARCHAR(50) NOT NULL DEFAULT 'Pending',
  `start_time` DATETIME NULL,
  `end_time` DATETIME NULL,
  `created_at` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  FOREIGN KEY (`batch_id`) REFERENCES `batches`(`id`) ON DELETE CASCADE,
  INDEX `idx_runs_batch` (`batch_id`),
  INDEX `idx_runs_status` (`status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
```

### 2.2. Nâng cấp bảng `batches` (Thêm cột)
```sql
ALTER TABLE `batches` ADD COLUMN `total_runs` INT NOT NULL DEFAULT 1 AFTER `status`;
```

### 2.3. Nâng cấp các bảng log (`alarmreport`, `alarmlog`, `realtime_alarms`)
```sql
ALTER TABLE `alarmreport` ADD COLUMN `runId` INT NULL DEFAULT NULL AFTER `batchId`;
ALTER TABLE `alarmlog` ADD COLUMN `runId` INT NULL DEFAULT NULL AFTER `batchId`;
ALTER TABLE `realtime_alarms` ADD COLUMN `runId` INT NULL DEFAULT NULL AFTER `batchId`;
```

### 2.4. Kịch bản Auto-Migration & Tương thích ngược (C# Code)
Khi khởi động, phương thức khởi tạo cơ sở dữ liệu trong `AlarmReportLogger` và `AlarmLogger` sẽ thực hiện các bước sau:
1. Đảm bảo bảng `batches` và cột `total_runs` tồn tại.
2. Tạo bảng `runs` nếu chưa có.
3. Thêm cột `runId` vào `alarmreport`, `alarmlog`, và `realtime_alarms`.
4. **Di chuyển dữ liệu cũ (One-time migration)**:
   ```sql
   -- 1. Tạo 1 mẻ mặc định cho mỗi lô cũ chưa có mẻ con trong bảng runs
   INSERT INTO `runs` (`batch_id`, `run_number`, `name`, `status`, `start_time`, `end_time`, `created_at`)
   SELECT b.id, 1, CONCAT(b.name, '-Run01'), b.status, b.start_time, b.end_time, b.created_at
   FROM `batches` b
   WHERE NOT EXISTS (SELECT 1 FROM `runs` r WHERE r.batch_id = b.id);

   -- 2. Cập nhật runId cho các báo cáo cũ đã có batchId
   UPDATE `alarmreport` ar
   JOIN `runs` r ON ar.batchId = r.batch_id
   SET ar.runId = r.id
   WHERE ar.runId IS NULL AND ar.batchId IS NOT NULL;

   -- 3. Cập nhật runId cho các cảnh báo sự cố cũ
   UPDATE `alarmlog` al
   JOIN `runs` r ON al.batchId = r.batch_id
   SET al.runId = r.id
   WHERE al.runId IS NULL AND al.batchId IS NOT NULL;
   ```

---

## 3. Thiết kế Nâng cấp HTTP API tự lưu trữ (`BatchesHttpServer`)

Lớp `BatchesHttpServer` được mở rộng để tiếp nhận và phản hồi dữ liệu chi tiết cho cả Batch và Run.

### 3.1. API `POST /api/batches/create` (Cập nhật)
API chấp nhận chèn một lô kèm theo nhiều mẻ sản xuất:
- **Body Input**:
  ```json
  {
    "device_name": "TX01",
    "runs_count": 2
  }
  ```
- **Xử lý phía Server**:
  ```csharp
  // 1. Tạo Batch
  string todayStr = DateTime.Now.ToString("yyyyMMdd");
  string batchName = $"{deviceName}-{todayStr}-{nextStt:D2}";
  // INSERT INTO batches (name, device_name, status, total_runs) VALUES (...)
  int batchId = (int)cmd.LastInsertedId;

  // 2. Tạo các mẻ con tương ứng theo số lượng runs_count
  for (int i = 1; i <= runsCount; i++)
  {
      string runName = $"{batchName}-Run{i:D2}";
      // INSERT INTO runs (batch_id, run_number, name, status) VALUES (@batchId, @i, @runName, 'Pending')
  }
  ```

### 3.2. API `GET /api/batches` (Tạo mới)
Trả về danh sách các Lô sản xuất của thiết bị:
- **Query Params**: `device_name` (string), `limit` (int, default 50).
- **SQL Query**:
  ```sql
  SELECT `id`, `name`, `device_name`, `status`, `total_runs`, `start_time`, `end_time`, `created_at` 
  FROM `batches` 
  WHERE `device_name` = @device_name 
  ORDER BY `id` DESC LIMIT @limit
  ```

### 3.3. API `GET /api/runs` (Tạo mới)
Trả về danh sách các Mẻ sản xuất thuộc về một Lô cụ thể phục vụ dropdown lọc cấp 2:
- **Query Params**: `batch_id` (int).
- **SQL Query**:
  ```sql
  SELECT `id`, `batch_id`, `run_number`, `name`, `status`, `start_time`, `end_time`, `created_at` 
  FROM `runs` 
  WHERE `batch_id` = @batchId 
  ORDER BY `run_number` ASC
  ```

---

## 4. Tích hợp State Machine trong `AlarmReportLogger` (C# Code)

Chúng ta cần cập nhật cơ chế giám sát mẻ trong `AlarmReportLogger.cs` như sau:

### 4.1. Khai báo các biến trạng thái trong bộ nhớ
```csharp
private int? activeBatchId = null; // ID lô hiện tại đang ghi log
private int? activeRunId = null;   // ID mẻ con hiện tại đang ghi log

public int? ActiveBatchId => activeBatchId;
public int? ActiveRunId => activeRunId;
```

### 4.2. Khởi tạo mẻ (FIFO) trong `LinkOrCreateActiveBatch`
```csharp
// 1. Kiểm tra xem có mẻ nào của thiết bị này đang ở trạng thái 'Active' không để liên kết lại (nếu khởi động lại ứng dụng)
string checkActiveRun = "SELECT r.id, r.batch_id FROM runs r " +
                        "JOIN batches b ON r.batch_id = b.id " +
                        $"WHERE b.device_name = '{deviceName}' AND r.status = 'Active' " +
                        "ORDER BY r.id DESC LIMIT 1";
var activeDt = dataAccess.ExecuteQuery(checkActiveRun);
if (activeDt != null && activeDt.Rows.Count > 0)
{
    activeRunId = Convert.ToInt32(activeDt.Rows[0]["id"]);
    activeBatchId = Convert.ToInt32(activeDt.Rows[0]["batch_id"]);
    return;
}

// 2. Tìm mẻ con 'Pending' cũ nhất thuộc lô 'Pending' hoặc 'Active' (FIFO)
string findPendingRun = "SELECT r.id, r.batch_id, b.status as batch_status FROM runs r " +
                        "JOIN batches b ON r.batch_id = b.id " +
                        $"WHERE b.device_name = '{deviceName}' AND r.status = 'Pending' " +
                        "ORDER BY b.id ASC, r.run_number ASC LIMIT 1";
var pendingDt = dataAccess.ExecuteQuery(findPendingRun);
if (pendingDt != null && pendingDt.Rows.Count > 0)
{
    int runId = Convert.ToInt32(pendingDt.Rows[0]["id"]);
    int batchId = Convert.ToInt32(pendingDt.Rows[0]["batch_id"]);
    string batchStatus = pendingDt.Rows[0]["batch_status"].ToString();

    // Cập nhật trạng thái mẻ con thành Active
    string updateRun = $"UPDATE runs SET status = 'Active', start_time = '{DateTime.Now:yyyy-MM-dd HH:mm:ss}' WHERE id = {runId}";
    dataAccess.ExecuteNonQuery(updateRun);

    // Nếu lô cha chưa Active, cập nhật lô cha thành Active
    if (batchStatus == "Pending")
    {
        string updateBatch = $"UPDATE batches SET status = 'Active', start_time = '{DateTime.Now:yyyy-MM-dd HH:mm:ss}' WHERE id = {batchId}";
        dataAccess.ExecuteNonQuery(updateBatch);
    }

    activeRunId = runId;
    activeBatchId = batchId;
}
else
{
    // 3. Fallback: Nếu không có mẻ nào chờ sẵn, tự sinh khẩn cấp (1 lô - 1 mẻ)
    string todayStr = DateTime.Now.ToString("yyyyMMdd");
    // Tự động INSERT lô khẩn cấp: total_runs = 1, status = 'Active'
    // Tự động INSERT mẻ khẩn cấp: run_number = 1, status = 'Active'
    // Lưu IDs vào activeBatchId, activeRunId
}
```

### 4.3. Hoàn thành mẻ con và kiểm tra kết thúc Lô trong `CompleteActiveBatch`
```csharp
if (activeRunId == null) return;

try
{
    dataAccess.ConnectionString = GetConnectionStringWithDb();
    string nowStr = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

    // 1. Cập nhật mẻ hiện tại thành Completed
    string completeRun = $"UPDATE runs SET status = 'Completed', end_time = '{nowStr}' WHERE id = {activeRunId.Value}";
    dataAccess.ExecuteNonQuery(completeRun);

    // 2. Kiểm tra xem lô cha còn mẻ con nào chưa hoàn thành không
    string checkRemaining = $"SELECT COUNT(*) FROM runs WHERE batch_id = {activeBatchId.Value} AND status != 'Completed'";
    var remainingObj = dataAccess.ExecuteScalarQuery(checkRemaining);
    int remainingCount = remainingObj != null ? Convert.ToInt32(remainingObj) : 0;

    if (remainingCount == 0)
    {
        // Tất cả mẻ con đã xong -> Cập nhật Batch thành Completed
        string completeBatch = $"UPDATE batches SET status = 'Completed', end_time = '{nowStr}' WHERE id = {activeBatchId.Value}";
        dataAccess.ExecuteNonQuery(completeBatch);
        System.Diagnostics.Debug.WriteLine($"[AlarmReportLogger] Completed Batch ID {activeBatchId.Value} along with all its runs.");
    }
    else
    {
        System.Diagnostics.Debug.WriteLine($"[AlarmReportLogger] Completed Run ID {activeRunId.Value}. Batch ID {activeBatchId.Value} remains Active ({remainingCount} run(s) remaining).");
    }
}
catch (Exception ex)
{
    System.Diagnostics.Debug.WriteLine($"[AlarmReportLogger] ERROR completing run: {ex.Message}");
}
finally
{
    activeRunId = null;
    activeBatchId = null;
}
```
