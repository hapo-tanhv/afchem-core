using ATSCADA;
using ATSCADA.ToolExtensions.ExtensionMethods;
using HinoTools.Data.Database;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

namespace HinoTools.Data.Log
{
    /// <summary>
    /// Mục đích lưu trữ cấu hình giám sát cho một thanh ghi cần kiểm tra ngưỡng.
    /// </summary>
    public class ThresholdItem
    {
        public ITag Tag { get; set; }

        public string TagName { get; set; }

        public string Alias { get; set; }

        /// <summary>Ngưỡng cảnh báo (ví dụ: 50 cho nhiệt độ hoặc giá trị tĩnh mặc định nếu so sánh với tag cài đặt)</summary>
        public double Threshold { get; set; }

        /// <summary>Tên tag cài đặt làm ngưỡng động hoặc rỗng</summary>
        public string ThresholdTagOrValue { get; set; }

        /// <summary>Đối tượng Tag của ngưỡng động</summary>
        public ITag ThresholdTag { get; set; }

        /// <summary>Toán tử so sánh: ">", "<", "="</summary>
        public string Operator { get; set; } = ">";

        /// <summary>Mức độ cảnh báo: ALARM | WARNING</summary>
        public string Severity { get; set; } = "ALARM";

        /// <summary>Mẫu thông điệp sự kiện</summary>
        public string EventMessageTemplate { get; set; } = "";

        public bool IsActive => Tag != null;
    }

    /// <summary>
    /// Component giám sát cảnh báo vượt ngưỡng tức thời (Realtime Threshold Alarm).
    /// Polling mỗi 3 giây, đọc giá trị các thanh ghi được cấu hình,
    /// kiểm tra ngưỡng với toán tử tùy chỉnh, và INSERT vào bảng realtime_alarms.
    /// </summary>
    public partial class RealtimeThresholdLogger : Component
    {
        #region FIELDS

        private DataAccess dataAccess;
        private System.Timers.Timer tmrScan;
        private iDriver driver;

        // Resolved tag references with threshold config
        private List<ThresholdItem> thresholdItems;
        private string deviceName;

        // Debounce: track which tags are currently in alarm state
        // Key = Alias, Value = true if currently alarming
        private Dictionary<string, bool> alarmActiveStates = new Dictionary<string, bool>();

        // Key = Alias, Value = ID of the active alarm record in realtime_alarms
        private Dictionary<string, int> activeAlarmRecordIds = new Dictionary<string, int>();

        #endregion

        #region PROPERTIES

        [Category("Hino Settings")]
        [Description("Reference to the AlarmReportLogger component to get active batch, stage and process details.")]
        public AlarmReportLogger AlarmReportLogger { get; set; }

        [Category("Hino Settings")]
        [Description("Select driver object.")]
        public iDriver Driver
        {
            get => driver;
            set
            {
                if (driver != null) driver.ConstructionCompleted -= Driver_ConstructionCompleted;
                driver = value;
                if (driver != null) driver.ConstructionCompleted += Driver_ConstructionCompleted;
                TryInitialize();
            }
        }

        [Category("Hino Settings")]
        public string ServerName { get; set; } = "localhost";

        [Category("Hino Settings")]
        public string UserID { get; set; } = "root";

        [Category("Hino Settings")]
        public string Password { get; set; } = "101101";

        [Category("Hino Settings")]
        public string DatabaseName { get; set; } = "scada";

        [Category("Hino Settings")]
        public string TableName { get; set; } = "realtime_alarms";

        [Category("Hino Settings")]
        [Description("Scanning interval in milliseconds (default: 3000ms = 3s).")]
        public int ScanInterval { get; set; } = 3000;

        private string[] _collection;

        [Category("Hino Settings")]
        [Description("Format: TagName;Alias;Threshold;Operator. " +
                      "Operator: > | < | = (default: >). " +
                      "Example: AFChemTX01.NhietDoMay;NhietDoMay;50;>")]
        public string[] Collection
        {
            get => _collection;
            set
            {
                _collection = value;
                TryInitialize();
            }
        }

        #endregion

        #region CONSTRUCTORS

        public RealtimeThresholdLogger()
        {
            InitializeComponent();
        }

        public RealtimeThresholdLogger(IContainer container)
        {
            container.Add(this);
            InitializeComponent();
        }

        #endregion

        #region INITIALIZATION

        private void Driver_ConstructionCompleted()
        {
            TryInitialize();
        }

        private void TryInitialize()
        {
            // Prevent initialization during Visual Studio Design-time to avoid hanging the Designer
            if (this.DesignMode || LicenseManager.UsageMode == LicenseUsageMode.Designtime) return;

            // Only initialize once, and only when both Driver and Collection are set
            if (tmrScan != null) return;
            if (driver == null || Collection == null || Collection.Length == 0) return;
            deviceName = ExtractDeviceName();
            thresholdItems = GetThresholdItems().ToList();
            if (thresholdItems.Count == 0) return;

            dataAccess = new DataAccess();
            tmrScan = new System.Timers.Timer();
            tmrScan.Interval = ScanInterval;
            tmrScan.AutoReset = false;
            tmrScan.Elapsed += (sender, e) => ScanAndLog();
            tmrScan.Start();
        }

        /// <summary>
        /// Parse Collection config into ThresholdItem list.
        /// Format: "TagName;Alias;Threshold;Operator;Severity;EventMessage"
        /// If Operator is omitted, defaults to ">".
        /// If Severity is omitted, defaults to "ALARM".
        /// If EventMessage is omitted, defaults to "".
        /// </summary>
        private IEnumerable<ThresholdItem> GetThresholdItems()
        {
            if (Collection == null) yield break;

            foreach (var item in Collection)
            {
                var parts = item.Split(';');
                if (parts.Length < 3) continue; // Minimum: TagName;Alias;Threshold

                string tagName = parts[0];
                string alias = "";
                double threshold = 0;
                string thresholdTagOrValue = "";
                string op = ">";
                string severity = "ALARM";
                string eventMessage = "";

                bool isFivePartsFormat = false;
                if (parts.Length == 5)
                {
                    // Check if parts[1] is a double and parts[2] is a operator (=, >, <)
                    double tempThreshold;
                    if (double.TryParse(parts[1], out tempThreshold))
                    {
                        string tempOp = parts[2].Trim();
                        if (tempOp == "=" || tempOp == ">" || tempOp == "<")
                        {
                            isFivePartsFormat = true;
                            threshold = tempThreshold;
                            op = tempOp;
                            alias = tagName + "_" + parts[1]; // Ensure unique alias e.g. AFChemTX01.MayLoi_1
                            severity = parts[3].Trim();
                            eventMessage = parts[4].Trim();
                        }
                    }
                }

                if (!isFivePartsFormat)
                {
                    alias = parts[1];
                    string thresholdStr = parts[2].Trim();
                    double tempThreshold;
                    if (double.TryParse(thresholdStr, NumberStyles.Float, CultureInfo.InvariantCulture, out tempThreshold))
                    {
                        threshold = tempThreshold;
                    }
                    else
                    {
                        // Dynamic threshold with fallback, e.g. DatNguongNhietDoMoiTruong:45
                        var thresholdParts = thresholdStr.Split(':');
                        thresholdTagOrValue = thresholdParts[0].Trim();
                        if (thresholdParts.Length > 1 && double.TryParse(thresholdParts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out tempThreshold))
                        {
                            threshold = tempThreshold;
                        }
                    }

                    op = parts.Length >= 4 ? parts[3].Trim() : ">";
                    if (op != ">" && op != "<" && op != "=")
                        op = ">"; // Fallback to default

                    severity = parts.Length >= 5 ? parts[4].Trim() : "ALARM";
                    eventMessage = parts.Length >= 6 ? parts[5].Trim() : "";
                }

                ITag thresholdTag = null;
                if (!string.IsNullOrEmpty(thresholdTagOrValue) && this.driver != null)
                {
                    string dev = string.IsNullOrEmpty(deviceName) ? ExtractDeviceName() : deviceName;
                    thresholdTag = this.driver.GetTagByName(thresholdTagOrValue) ?? 
                                   this.driver.GetTagByName(dev + "." + thresholdTagOrValue);
                }

                yield return new ThresholdItem()
                {
                    TagName = tagName,
                    Tag = this.driver?.GetTagByName(tagName),
                    Alias = alias,
                    Threshold = threshold,
                    ThresholdTagOrValue = thresholdTagOrValue,
                    ThresholdTag = thresholdTag,
                    Operator = op,
                    Severity = severity,
                    EventMessageTemplate = eventMessage
                };
            }
        }

        /// <summary>
        /// Extract device name prefix from first tag (e.g. "AFChemTX01.NhietDoMay" -> "AFChemTX01").
        /// </summary>
        private string ExtractDeviceName()
        {
            if (Collection == null || Collection.Length == 0)
                return "Unknown";

            var firstTag = Collection[0].Split(';')[0];
            var dotIndex = firstTag.IndexOf('.');
            return dotIndex > 0 ? firstTag.Substring(0, dotIndex) : firstTag;
        }

        #endregion

        #region SCANNING & THRESHOLD LOGIC

        private int GetSeverityRank(string severity)
        {
            if (string.IsNullOrEmpty(severity)) return 0;
            switch (severity.ToUpperInvariant())
            {
                case "INFO": return 1;
                case "LOW": return 2;
                case "AVERAGE": return 3;
                case "HIGH": return 4;
                default: return 0;
            }
        }

        private double GetCurrentThresholdValue(ThresholdItem item)
        {
            double thresholdVal = item.Threshold;

            if (item.ThresholdTag == null && !string.IsNullOrEmpty(item.ThresholdTagOrValue) && driver != null)
            {
                item.ThresholdTag = driver.GetTagByName(item.ThresholdTagOrValue) ?? 
                                    driver.GetTagByName(deviceName + "." + item.ThresholdTagOrValue);
            }

            if (item.ThresholdTag != null && item.ThresholdTag.Value != null)
            {
                double rawThreshold;
                if (double.TryParse(item.ThresholdTag.Value, out rawThreshold))
                {
                    if (item.ThresholdTagOrValue.EndsWith("DatNguongNhietDoMoiTruong", StringComparison.OrdinalIgnoreCase) ||
                        item.ThresholdTagOrValue.EndsWith("DatNguongDoAmMoiTruong", StringComparison.OrdinalIgnoreCase) ||
                        item.ThresholdTagOrValue.EndsWith("CaiDatApSuat", StringComparison.OrdinalIgnoreCase))
                    {
                        if (item.ThresholdTagOrValue.EndsWith("CaiDatApSuat", StringComparison.OrdinalIgnoreCase))
                        {
                            rawThreshold = Math.Round(rawThreshold / 100.0, 2);
                        }
                        else
                        {
                            rawThreshold = Math.Round(rawThreshold / 10.0, 2);
                        }
                    }
                    thresholdVal = rawThreshold;
                }
            }

            return thresholdVal;
        }

        /// <summary>
        /// Core scan method called every 3 seconds.
        /// Reads all configured tags and checks thresholds.
        /// </summary>
        private void ScanAndLog()
        {
            try
            {
                tmrScan.Stop();

                // 1. First pass: Determine the highest violating severity rank for each TagName
                var highestViolatingRankByTag = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in thresholdItems)
                {
                    if (!item.IsActive) continue;
                    if (item.Tag == null && driver != null)
                    {
                        item.Tag = driver.GetTagByName(item.TagName);
                    }

                    if (item.Tag?.Value == null) continue;
                    double currentValue;
                    if (!double.TryParse(item.Tag.Value, out currentValue)) continue;

                    if (string.Equals(item.Alias, "CaiDatApSuat", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(item.Alias, "ApSuat", StringComparison.OrdinalIgnoreCase))
                    {
                        currentValue = Math.Round(currentValue / 100.0, 2);
                    }
                    else if (string.Equals(item.Alias, "DatNguongNhietDoMoiTruong", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(item.Alias, "DatNguongDoAmMoiTruong", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(item.Alias, "NhietDoMoiTruong", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(item.Alias, "DoAmMoiTruong", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(item.Alias, "NhietDoBonTronTren", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(item.Alias, "NhietDoBonTronGiua", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(item.Alias, "NhietDoBonTronDuoi", StringComparison.OrdinalIgnoreCase))
                    {
                        currentValue = Math.Round(currentValue / 10.0, 2);
                    }

                    double thresholdValue = GetCurrentThresholdValue(item);
                    if (EvaluateThreshold(currentValue, item.Operator, thresholdValue))
                    {
                        int rank = GetSeverityRank(item.Severity);
                        string key = item.TagName;
                        if (!highestViolatingRankByTag.TryGetValue(key, out int currentMax) || rank > currentMax)
                        {
                            highestViolatingRankByTag[key] = rank;
                        }
                    }
                }

                // 2. Second pass: Process edge state changes
                foreach (var item in thresholdItems)
                {
                    if (!item.IsActive) continue;

                    // Dynamically resolve tag if it was null at startup
                    if (item.Tag == null && driver != null)
                    {
                        item.Tag = driver.GetTagByName(item.TagName);
                    }

                    double currentValue;
                    if (item.Tag?.Value == null) continue;
                    if (!double.TryParse(item.Tag.Value, out currentValue)) continue;

                    if (string.Equals(item.Alias, "CaiDatApSuat", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(item.Alias, "ApSuat", StringComparison.OrdinalIgnoreCase))
                    {
                        currentValue = Math.Round(currentValue / 100.0, 2);
                    }
                    else if (string.Equals(item.Alias, "DatNguongNhietDoMoiTruong", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(item.Alias, "DatNguongDoAmMoiTruong", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(item.Alias, "NhietDoMoiTruong", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(item.Alias, "DoAmMoiTruong", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(item.Alias, "NhietDoBonTronTren", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(item.Alias, "NhietDoBonTronGiua", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(item.Alias, "NhietDoBonTronDuoi", StringComparison.OrdinalIgnoreCase))
                    {
                        currentValue = Math.Round(currentValue / 10.0, 2);
                    }

                    double thresholdValue = GetCurrentThresholdValue(item);
                    bool isPhysicallyViolating = EvaluateThreshold(currentValue, item.Operator, thresholdValue);
                    int itemRank = GetSeverityRank(item.Severity);
                    highestViolatingRankByTag.TryGetValue(item.TagName, out int maxRankForTag);

                    bool isViolating = isPhysicallyViolating && (itemRank == maxRankForTag);
                    bool wasAlarming;
                    alarmActiveStates.TryGetValue(item.Alias, out wasAlarming);

                    if (isViolating && !wasAlarming)
                    {
                        // Rising edge: first violation -> INSERT and mark as alarming
                        int alarmId = InsertRealtimeAlarm(item, currentValue, thresholdValue);
                        alarmActiveStates[item.Alias] = true;
                        if (alarmId > 0)
                        {
                            activeAlarmRecordIds[item.Alias] = alarmId;
                        }
                    }
                    else if (!isViolating && wasAlarming)
                    {
                        // Falling edge: value returned to safe -> reset state and set restore_time
                        int alarmId;
                        if (activeAlarmRecordIds.TryGetValue(item.Alias, out alarmId) && alarmId > 0)
                        {
                            UpdateRealtimeAlarmRestoreTime(alarmId);
                            activeAlarmRecordIds.Remove(item.Alias);
                        }
                        else
                        {
                            // Fallback if ID wasn't in memory
                            UpdateLastActiveAlarmRestoreTime(item.Alias);
                        }
                        alarmActiveStates[item.Alias] = false;
                    }
                    // If still violating (wasAlarming && isViolating): do nothing (debounce)
                }

                tmrScan.Start();
            }
            catch
            {
                tmrScan.Start();
            }
        }

        /// <summary>
        /// Evaluate: Value [Operator] Threshold.
        /// Supports ">", "<", "=".
        /// </summary>
        private bool EvaluateThreshold(double value, string op, double threshold)
        {
            switch (op)
            {
                case ">": return value > threshold;
                case "<": return value < threshold;
                case "=": return Math.Abs(value - threshold) < 0.001;
                default: return value > threshold;
            }
        }

        #endregion

        #region DATABASE OPERATIONS

        private string GetConnectionStringWithoutDb()
        {
            return $"Server={ServerName};Uid={UserID};Pwd={Password};";
        }

        private string GetConnectionStringWithDb()
        {
            return $"Server={ServerName};Uid={UserID};Pwd={Password};Database={DatabaseName}";
        }

        /// <summary>
        /// Create the database if it doesn't exist.
        /// </summary>
        public bool CreateDatabaseIfNotExists()
        {
            try
            {
                dataAccess.ConnectionString = GetConnectionStringWithoutDb();
                var query = $"CREATE DATABASE IF NOT EXISTS `{DatabaseName}`";
                return dataAccess.ExecuteNonQuery(query) >= 0;
            }
            catch { return false; }
        }

        /// <summary>
        /// Create the realtime_alarms table.
        /// Columns: ID, DateTime, DeviceName, TagName, Value, Threshold, Operator, Message, QuyTrinh, CongDoan, batchId, Severity, restore_time.
        /// </summary>
        public bool CreateTableIfNotExists()
        {
            try
            {
                dataAccess.ConnectionString = GetConnectionStringWithDb();

                var query = $"CREATE TABLE IF NOT EXISTS `{TableName}` (" +
                    "`ID` INT AUTO_INCREMENT PRIMARY KEY, " +
                    "`DateTime` DATETIME NOT NULL, " +
                    "`DeviceName` VARCHAR(100) NOT NULL, " +
                    "`TagName` VARCHAR(200) NOT NULL, " +
                    "`Value` DOUBLE NOT NULL DEFAULT 0, " +
                    "`Threshold` DOUBLE NOT NULL DEFAULT 0, " +
                    "`Operator` VARCHAR(5) NOT NULL DEFAULT '>', " +
                    "`Message` VARCHAR(500) NOT NULL DEFAULT '', " +
                    "`QuyTrinh` INT NOT NULL DEFAULT 0, " +
                    "`CongDoan` VARCHAR(100) NOT NULL DEFAULT '', " +
                    "`batchId` INT NULL DEFAULT NULL, " +
                    "`runId` INT NULL DEFAULT NULL, " +
                    "`Severity` VARCHAR(50) NOT NULL DEFAULT 'ALARM', " +
                    "`restore_time` DATETIME NULL DEFAULT NULL" +
                    ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";

                bool success = dataAccess.ExecuteNonQuery(query) >= 0;
                if (!success) return false;

                // Add columns just in case the table existed without them
                string[] checkCols = { "QuyTrinh", "CongDoan", "batchId", "runId", "Severity", "restore_time" };
                string[] alterSqls = {
                    $"ALTER TABLE `{TableName}` ADD COLUMN `QuyTrinh` INT NOT NULL DEFAULT 0",
                    $"ALTER TABLE `{TableName}` ADD COLUMN `CongDoan` VARCHAR(100) NOT NULL DEFAULT ''",
                    $"ALTER TABLE `{TableName}` ADD COLUMN `batchId` INT NULL DEFAULT NULL",
                    $"ALTER TABLE `{TableName}` ADD COLUMN `runId` INT NULL DEFAULT NULL AFTER `batchId`",
                    $"ALTER TABLE `{TableName}` ADD COLUMN `Severity` VARCHAR(50) NOT NULL DEFAULT 'ALARM'",
                    $"ALTER TABLE `{TableName}` ADD COLUMN `restore_time` DATETIME NULL DEFAULT NULL"
                };

                for (int i = 0; i < checkCols.Length; i++)
                {
                    try
                    {
                        string checkQuery = $"SHOW COLUMNS FROM `{TableName}` LIKE '{checkCols[i]}'";
                        var res = dataAccess.ExecuteScalarQuery(checkQuery);
                        if (res == null || res == DBNull.Value)
                        {
                            dataAccess.ExecuteNonQuery(alterSqls[i]);
                            if (checkCols[i] == "runId")
                            {
                                // Migrate existing realtime alarm records based on batchId
                                string migrateLogsSql = $"UPDATE `{TableName}` t " +
                                                        "JOIN `runs` r ON t.batchId = r.batch_id " +
                                                        "SET t.runId = r.id " +
                                                        "WHERE t.runId IS NULL AND t.batchId IS NOT NULL";
                                dataAccess.ExecuteNonQuery(migrateLogsSql);
                            }
                        }
                    }
                    catch { }
                }

                return true;
            }
            catch { return false; }
        }

        /// <summary>
        /// Insert a threshold violation record.
        /// Returns the ID of the inserted record.
        /// </summary>
        private int InsertRealtimeAlarm(ThresholdItem item, double currentValue, double actualThreshold)
        {
            try
            {
                if (!CreateDatabaseIfNotExists()) return 0;
                if (!CreateTableIfNotExists()) return 0;

                dataAccess.ConnectionString = GetConnectionStringWithDb();

                var valStr = currentValue.ToString(CultureInfo.InvariantCulture);
                var threshStr = actualThreshold.ToString(CultureInfo.InvariantCulture);
                
                string message;
                if (!string.IsNullOrEmpty(item.EventMessageTemplate))
                {
                    message = $"{item.EventMessageTemplate} Giá trị: {valStr} (ngưỡng: {threshStr})";
                }
                else
                {
                    message = $"{item.Alias} = {valStr} {item.Operator} {threshStr}";
                }

                // Exposing values from AlarmReportLogger if linked
                int quyTrinh = 0;
                string congDoan = "IDLE";
                string batchIdValue = "NULL";
                string runIdValue = "NULL";

                if (AlarmReportLogger != null)
                {
                    quyTrinh = AlarmReportLogger.CurrentQuyTrinh;
                    congDoan = AlarmReportLogger.CurrentCongDoanCode ?? "IDLE";
                    if (AlarmReportLogger.ActiveBatchId.HasValue)
                    {
                        batchIdValue = AlarmReportLogger.ActiveBatchId.Value.ToString();
                    }
                    else if (AlarmReportLogger.LastActiveBatchId.HasValue && (DateTime.Now - AlarmReportLogger.LastActiveRunEndTime).TotalSeconds <= 10)
                    {
                        batchIdValue = AlarmReportLogger.LastActiveBatchId.Value.ToString();
                    }

                    if (AlarmReportLogger.ActiveRunId.HasValue)
                    {
                        runIdValue = AlarmReportLogger.ActiveRunId.Value.ToString();
                    }
                    else if (AlarmReportLogger.LastActiveRunId.HasValue && (DateTime.Now - AlarmReportLogger.LastActiveRunEndTime).TotalSeconds <= 10)
                    {
                        runIdValue = AlarmReportLogger.LastActiveRunId.Value.ToString();
                    }
                }

                var query = $"INSERT INTO `{TableName}` " +
                    $"(`DateTime`, `DeviceName`, `TagName`, `Value`, `Threshold`, `Operator`, `Message`, `QuyTrinh`, `CongDoan`, `batchId`, `runId`, `Severity`) " +
                    $"VALUES (" +
                    $"'{DateTime.Now:yyyy-MM-dd HH:mm:ss}', " +
                    $"'{deviceName}', " +
                    $"'{item.Alias}', " +
                    $"{valStr}, " +
                    $"{threshStr}, " +
                    $"'{item.Operator}', " +
                    $"'{message}', " +
                    $"{quyTrinh}, " +
                    $"'{congDoan}', " +
                    $"{batchIdValue}, " +
                    $"{runIdValue}, " +
                    $"'{item.Severity}'); SELECT LAST_INSERT_ID();";

                var result = dataAccess.ExecuteScalarQuery(query);
                if (result != null && result != DBNull.Value)
                {
                    return Convert.ToInt32(result);
                }
                return 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RealtimeThresholdLogger] InsertRealtimeAlarm ERROR: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Update the restore_time of a specific active alarm record by ID.
        /// </summary>
        private bool UpdateRealtimeAlarmRestoreTime(int alarmId)
        {
            try
            {
                dataAccess.ConnectionString = GetConnectionStringWithDb();
                string query = $"UPDATE `{TableName}` SET `restore_time` = '{DateTime.Now:yyyy-MM-dd HH:mm:ss}' WHERE `ID` = {alarmId}";
                return dataAccess.ExecuteNonQuery(query) >= 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RealtimeThresholdLogger] UpdateRealtimeAlarmRestoreTime ERROR: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Fallback: Find the latest active alarm for this tag (where restore_time is NULL) and set its restore_time.
        /// </summary>
        private bool UpdateLastActiveAlarmRestoreTime(string tagAlias)
        {
            try
            {
                dataAccess.ConnectionString = GetConnectionStringWithDb();
                string findQuery = $"SELECT `ID` FROM `{TableName}` WHERE `TagName` = '{tagAlias}' AND `restore_time` IS NULL ORDER BY `ID` DESC LIMIT 1";
                var result = dataAccess.ExecuteScalarQuery(findQuery);
                if (result != null && result != DBNull.Value)
                {
                    int lastId = Convert.ToInt32(result);
                    return UpdateRealtimeAlarmRestoreTime(lastId);
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RealtimeThresholdLogger] UpdateLastActiveAlarmRestoreTime ERROR: {ex.Message}");
                return false;
            }
        }

        #endregion
    }
}
