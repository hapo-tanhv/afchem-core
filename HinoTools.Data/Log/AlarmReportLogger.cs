using ATSCADA;
using ATSCADA.ToolExtensions.ExtensionMethods;
using HinoTools.Data.Database;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace HinoTools.Data.Log
{
    /// <summary>
    /// Component ghi log báo cáo mẻ trộn (Alarm Report) theo chu kỳ 30 giây.
    /// Tự động bắt đầu ghi khi phát hiện ThoiGianCapLieu > 0 (mẻ mới),
    /// tăng QuyTrinh (+1), và dừng ghi khi ThoiGianRungXaHang nhảy về 0.
    /// </summary>
    public partial class AlarmReportLogger : Component
    {
        #region FIELDS

        private DataAccess dataAccess;
        private System.Timers.Timer tmrLog;
        private iDriver driver;

        // State Machine fields
        private int currentCongDoan = 0; // 0 = Idle, 1-5 = in progress
        private int currentQuyTrinh = 0;
        private string deviceName;

        // Tracking flags for transitions (>0 to 0)
        private bool hasThoiGianCapLieuStarted = false;
        private bool hasThoiGianTron1Started = false;
        private bool hasThoiGianHutXaStarted = false;
        private bool hasThoiGianTron2Started = false;
        private bool hasThoiGianRungXaHangStarted = false;
        private bool hasThoiGianXaHangStarted = false;
        
        private bool isThoiGianRungXaHangFinished = false;
        private bool isThoiGianXaHangFinished = false;

        // Tag references (resolved once at startup)
        private List<LogItem> logItems;

        // Batches and API fields
        private HinoTools.Data.Http.BatchesHttpServer httpServer;
        private int? activeBatchId = null;

        private DateTime lastAlarmReportTime = DateTime.MinValue;

        #endregion

        #region PROPERTIES

        public int CurrentCongDoan => currentCongDoan;
        public int CurrentQuyTrinh => currentQuyTrinh;
        public int? ActiveBatchId => activeBatchId;

        public string CurrentCongDoanName
        {
            get
            {
                switch (currentCongDoan)
                {
                    case 1:
                        return "Cấp liệu";
                    case 2:
                        return "Trộn 1";
                    case 3:
                        double thoiGianRungXaDay = GetTagValueByAlias("ThoiGianRungXaDay");
                        double thoiGianHutXaDay = GetTagValueByAlias("ThoiGianHutXaDay");
                        if (thoiGianRungXaDay > 0) return "Rung xả đáy";
                        if (thoiGianHutXaDay > 0) return "Hút xả đáy";
                        return "Xả đáy";
                    case 4:
                        return "Trộn 2";
                    case 5:
                        double thoiGianRungXaHang = GetTagValueByAlias("ThoiGianRungXaHang");
                        if (thoiGianRungXaHang > 0) return "Rung xả hàng";
                        return "Xả hàng";
                    default:
                        return "Idle";
                }
            }
        }

        public string CurrentCongDoanCode
        {
            get
            {
                switch (currentCongDoan)
                {
                    case 1:
                        return "T001";
                    case 2:
                        return "T002";
                    case 3:
                        double thoiGianRungXaDay = GetTagValueByAlias("ThoiGianRungXaDay");
                        double thoiGianHutXaDay = GetTagValueByAlias("ThoiGianHutXaDay");
                        if (thoiGianRungXaDay > 0) return "T004";
                        if (thoiGianHutXaDay > 0) return "T005";
                        return "T003";
                    case 4:
                        return "T006";
                    case 5:
                        double thoiGianRungXaHang = GetTagValueByAlias("ThoiGianRungXaHang");
                        if (thoiGianRungXaHang > 0) return "T008";
                        return "T007";
                    default:
                        return "IDLE";
                }
            }
        }

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
        public string TableName { get; set; } = "alarmreport";

        [Category("Hino Settings")]
        [Description("Polling interval in milliseconds (default: 30000ms = 30s).")]
        public int PollingInterval { get; set; } = 30000;

        [Category("Hino Settings")]
        [Description("HTTP Server API Port (default: 5500).")]
        public int HttpPort { get; set; } = 5500;

        private string[] _collection;

        [Category("Hino Settings")]
        [Description("Format: TagName;Alias (17 registers + CongDoanMay). " +
                      "Example: AFChemTX01.ThoiGianCapLieu;ThoiGianCapLieu")]
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

        public AlarmReportLogger()
        {
            InitializeComponent();
        }

        public AlarmReportLogger(IContainer container)
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
            if (tmrLog != null) return;
            if (driver == null || Collection == null || Collection.Length == 0) return;

            logItems = GetLogItems().ToList();
            if (logItems.Count == 0) return;

            // Extract DeviceName from the first tag's full name dynamically
            deviceName = ExtractDeviceName();

            dataAccess = new DataAccess();

            // Auto-Migration
            CreateDatabaseIfNotExists();
            EnsureBatchesTableExists();
            CreateTableIfNotExists();
            AddBatchIdColumnIfNeeded(TableName);

            // Start HTTP API Server
            try
            {
                httpServer = new HinoTools.Data.Http.BatchesHttpServer(GetConnectionStringWithDb(), HttpPort);
                httpServer.Start();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AlarmReportLogger] HTTP Server start FAILED: {ex.Message}");
            }

            tmrLog = new System.Timers.Timer();
            // Start with a fast 1-second interval to catch the exact moment the batch starts
            tmrLog.Interval = 1000; 
            tmrLog.AutoReset = false;
            tmrLog.Elapsed += (sender, e) => PollAndLog();
            tmrLog.Start();
            System.Diagnostics.Debug.WriteLine($"[AlarmReportLogger] Initialized successfully. Idle Timer started (1000ms).");
        }

        private IEnumerable<LogItem> GetLogItems()
        {
            if (Collection == null) yield break;

            foreach (var item in Collection)
            {
                var itemSplit = item.Split(';');
                if (itemSplit.Length != 2) continue;

                yield return new LogItem()
                {
                    TagName = itemSplit[0],
                    Tag = this.driver?.GetTagByName(itemSplit[0]),
                    Alias = itemSplit[1]
                };
            }
        }

        /// <summary>
        /// Extract the device name prefix dynamically.
        /// </summary>
        private string ExtractDeviceName()
        {
            if (Collection == null || Collection.Length == 0)
                return "Unknown";

            var firstTag = Collection[0].Split(';')[0]; // e.g. "AFChemTX01.ThoiGianCapLieu"
            return HinoTools.Data.Helper.DeviceNameHelper.ExtractDeviceName(firstTag);
        }

        #endregion

        #region STATE MACHINE & POLLING

        /// <summary>
        /// Core polling method called every 30 seconds.
        /// Implements the State Machine for batch lifecycle detection.
        /// </summary>
        private void PollAndLog()
        {
            try
            {
                tmrLog.Stop();

                double thoiGianCapLieu = GetTagValueByAlias("ThoiGianCapLieu");
                double thoiGianTron1 = GetTagValueByAlias("ThoiGianTron1");
                double thoiGianHutXa = GetTagValueByAlias("ThoiGianHutXaDay");
                double thoiGianTron2 = GetTagValueByAlias("ThoiGianTron2");
                double thoiGianRungXaHang = GetTagValueByAlias("ThoiGianRungXaHang");
                double thoiGianXaHang = GetTagValueByAlias("ThoiGianXaHang");

                System.Diagnostics.Debug.WriteLine($"[AlarmReportLogger] Poll: CongDoan={currentCongDoan}, QuyTrinh={currentQuyTrinh}");

                int previousCongDoan = currentCongDoan;

                if (currentCongDoan == 1)
                {
                    if (thoiGianCapLieu > 0) hasThoiGianCapLieuStarted = true;
                    if (hasThoiGianCapLieuStarted && thoiGianCapLieu == 0)
                    {
                        currentCongDoan = 2;
                        InsertRealtimeInfoEvent("T002", "Bắt đầu trộn lần 1");
                    }
                }
                
                if (currentCongDoan == 2)
                {
                    if (thoiGianTron1 > 0) hasThoiGianTron1Started = true;
                    if (hasThoiGianTron1Started && thoiGianTron1 == 0)
                    {
                        currentCongDoan = 3;
                        InsertRealtimeInfoEvent("T003", "Bắt đầu xả đáy");
                    }
                }

                if (currentCongDoan == 3)
                {
                    if (thoiGianHutXa > 0) hasThoiGianHutXaStarted = true;
                    if (hasThoiGianHutXaStarted && thoiGianHutXa == 0)
                    {
                        currentCongDoan = 4;
                        InsertRealtimeInfoEvent("T006", "Bắt đầu trộn lần 2");
                    }
                }

                if (currentCongDoan == 4)
                {
                    if (thoiGianTron2 > 0) hasThoiGianTron2Started = true;
                    if (hasThoiGianTron2Started && thoiGianTron2 == 0)
                    {
                        currentCongDoan = 5;
                        InsertRealtimeInfoEvent("T007", "Bắt đầu xả hàng");
                    }
                }

                if (currentCongDoan == 5)
                {
                    if (thoiGianRungXaHang > 0) hasThoiGianRungXaHangStarted = true;
                    if (thoiGianXaHang > 0) hasThoiGianXaHangStarted = true;

                    if (hasThoiGianRungXaHangStarted && thoiGianRungXaHang == 0) isThoiGianRungXaHangFinished = true;
                    if (hasThoiGianXaHangStarted && thoiGianXaHang == 0) isThoiGianXaHangFinished = true;

                    if (isThoiGianRungXaHangFinished && isThoiGianXaHangFinished)
                    {
                        currentCongDoan = 0; // Return to Idle
                        InsertRealtimeInfoEvent("IDLE", "Xả liệu hoàn tất");
                        CompleteActiveBatch();
                    }
                }

                if (currentCongDoan == 0) // Idle
                {
                    if (thoiGianCapLieu > 0)
                    {
                        // New batch detected
                        currentQuyTrinh = GetMaxQuyTrinhFromDb() + 1;
                        currentCongDoan = 1;
                        ResetFlags();
                        hasThoiGianCapLieuStarted = true;
                        LinkOrCreateActiveBatch();
                        InsertRealtimeInfoEvent("T001", "Bắt đầu cấp liệu");
                        lastAlarmReportTime = DateTime.MinValue; // Ensure first row logs instantly
                    }
                }

                // Logging decision with database throttling to avoid bloat
                if (currentCongDoan > 0)
                {
                    bool shouldLog = false;
                    if (lastAlarmReportTime == DateTime.MinValue)
                    {
                        shouldLog = true;
                    }
                    else
                    {
                        double elapsedMs = (DateTime.Now - lastAlarmReportTime).TotalMilliseconds;
                        if (elapsedMs >= PollingInterval)
                        {
                            shouldLog = true;
                        }
                    }

                    if (shouldLog)
                    {
                        if (InsertAlarmReport())
                        {
                            lastAlarmReportTime = DateTime.Now;
                        }
                    }
                }
                else if (previousCongDoan == 5 && currentCongDoan == 0)
                {
                    // Batch ended in this tick AND new batch didn't start yet.
                    // Log the final zero values with CongDoan = 5, bypassing throttle to capture end-state immediately.
                    currentCongDoan = 5;
                    InsertAlarmReport();
                    currentCongDoan = 0;

                    // Reset throttle tracker so the next batch logs its first row instantly
                    lastAlarmReportTime = DateTime.MinValue;
                }

                // Keep polling interval at 1 second always to monitor stage transitions in real-time
                tmrLog.Interval = 1000;
                tmrLog.Start();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AlarmReportLogger] ERROR in PollAndLog: {ex.Message}");
                tmrLog.Start();
            }
        }

        private void ResetFlags()
        {
            hasThoiGianCapLieuStarted = false;
            hasThoiGianTron1Started = false;
            hasThoiGianHutXaStarted = false;
            hasThoiGianTron2Started = false;
            hasThoiGianRungXaHangStarted = false;
            hasThoiGianXaHangStarted = false;
            isThoiGianRungXaHangFinished = false;
            isThoiGianXaHangFinished = false;
        }

        /// <summary>
        /// Read a tag value by its Alias name (e.g. "ThoiGianCapLieu").
        /// Returns 0 if not found or not parseable.
        /// </summary>
        private double GetTagValueByAlias(string alias)
        {
            var item = logItems.FirstOrDefault(x =>
                string.Equals(x.Alias, alias, StringComparison.OrdinalIgnoreCase));

            if (item == null) return 0;

            // Dynamically resolve tag if it was null at startup (e.g. ATDriverClient late connection)
            if (item.Tag == null && driver != null)
            {
                item.Tag = driver.GetTagByName(item.TagName);
                if (item.Tag == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[AlarmReportLogger] WARNING: Tag '{item.TagName}' not found in driver!");
                }
            }

            if (item.Tag?.Value == null) return 0;

            double.TryParse(item.Tag.Value, out double val);
            return val;
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

        private void AddBatchIdColumnIfNeeded(string tableName)
        {
            try
            {
                dataAccess.ConnectionString = GetConnectionStringWithDb();
                string checkQuery = $"SHOW COLUMNS FROM `{tableName}` LIKE 'batchId'";
                var result = dataAccess.ExecuteScalarQuery(checkQuery);
                if (result == null || result == DBNull.Value)
                {
                    string alterQuery = $"ALTER TABLE `{tableName}` ADD COLUMN `batchId` INT NULL DEFAULT NULL";
                    dataAccess.ExecuteNonQuery(alterQuery);
                    System.Diagnostics.Debug.WriteLine($"[Migration] Added batchId column to {tableName} successfully.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Migration] ERROR adding batchId column to {tableName}: {ex.Message}");
            }
        }

        private void EnsureBatchesTableExists()
        {
            try
            {
                dataAccess.ConnectionString = GetConnectionStringWithDb();
                string createTableSql = "CREATE TABLE IF NOT EXISTS `batches` (" +
                                        "  `id` INT AUTO_INCREMENT PRIMARY KEY," +
                                        "  `name` VARCHAR(100) NOT NULL UNIQUE," +
                                        "  `device_name` VARCHAR(100) NOT NULL," +
                                        "  `status` VARCHAR(50) NOT NULL DEFAULT 'Pending'," +
                                        "  `start_time` DATETIME NULL," +
                                        "  `end_time` DATETIME NULL," +
                                        "  `created_at` TIMESTAMP DEFAULT CURRENT_TIMESTAMP" +
                                        ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";
                dataAccess.ExecuteNonQuery(createTableSql);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Migration] ERROR ensuring batches table: {ex.Message}");
            }
        }

        private void LinkOrCreateActiveBatch()
        {
            try
            {
                dataAccess.ConnectionString = GetConnectionStringWithDb();

                // 0. Check if there is already an Active batch for this device (prevents race condition with AlarmLogger)
                string activeQuery = $"SELECT `id` FROM `batches` " +
                                     $"WHERE `device_name` = '{deviceName}' AND `status` = 'Active' " +
                                     $"ORDER BY `id` DESC LIMIT 1";
                var activeResult = dataAccess.ExecuteScalarQuery(activeQuery);
                if (activeResult != null && activeResult != DBNull.Value)
                {
                    activeBatchId = Convert.ToInt32(activeResult);
                    System.Diagnostics.Debug.WriteLine($"[AlarmReportLogger] Already found an Active batch (ID: {activeBatchId.Value}). Linking to it.");
                    return;
                }
                
                // 1. Find the oldest 'Pending' batch for this device (FIFO)
                string findQuery = $"SELECT `id`, `name` FROM `batches` " +
                                   $"WHERE `device_name` = '{deviceName}' AND `status` = 'Pending' " +
                                   $"ORDER BY `id` ASC LIMIT 1";
                
                var dt = dataAccess.ExecuteQuery(findQuery);
                if (dt != null && dt.Rows.Count > 0)
                {
                    int batchId = Convert.ToInt32(dt.Rows[0]["id"]);
                    string batchName = dt.Rows[0]["name"].ToString();
                    
                    // Update this batch to Active and set start_time
                    string updateQuery = $"UPDATE `batches` " +
                                         $"SET `status` = 'Active', `start_time` = '{DateTime.Now:yyyy-MM-dd HH:mm:ss}' " +
                                         $"WHERE `id` = {batchId}";
                    dataAccess.ExecuteNonQuery(updateQuery);
                    
                    activeBatchId = batchId;
                    System.Diagnostics.Debug.WriteLine($"[AlarmReportLogger] FIFO linked batch '{batchName}' (ID: {batchId}) as Active.");
                }
                else
                {
                    // 2. If no Pending batch exists, auto-generate a fallback/emergency batch
                    string todayStr = DateTime.Now.ToString("yyyyMMdd");
                    int nextStt = 1;
                    
                    // Find the last batch created today for this device
                    string selectLastQuery = $"SELECT `name` FROM `batches` " +
                                            $"WHERE `device_name` = '{deviceName}' AND DATE(`created_at`) = CURDATE() " +
                                            $"ORDER BY `id` DESC LIMIT 1";
                    var lastObj = dataAccess.ExecuteScalarQuery(selectLastQuery);
                    if (lastObj != null && lastObj != DBNull.Value)
                    {
                        var parts = lastObj.ToString().Split('-');
                        if (parts.Length >= 3 && int.TryParse(parts[parts.Length - 1], out int lastStt))
                        {
                            nextStt = lastStt + 1;
                        }
                    }
                    
                    string fallbackBatchName = $"{deviceName}-{todayStr}-{nextStt:D2}";
                    
                    // Insert and mark as Active immediately
                    string insertQuery = $"INSERT INTO `batches` (`name`, `device_name`, `status`, `start_time`, `created_at`) " +
                                         $"VALUES ('{fallbackBatchName}', '{deviceName}', 'Active', '{DateTime.Now:yyyy-MM-dd HH:mm:ss}', NOW())";
                    dataAccess.ExecuteNonQuery(insertQuery);
                    
                    // Get the last inserted ID
                    string getLastIdQuery = "SELECT LAST_INSERT_ID()";
                    var lastIdObj = dataAccess.ExecuteScalarQuery(getLastIdQuery);
                    if (lastIdObj != null && lastIdObj != DBNull.Value)
                    {
                        activeBatchId = Convert.ToInt32(lastIdObj);
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"[AlarmReportLogger] No Pending batch found. Created fallback Active batch '{fallbackBatchName}' (ID: {activeBatchId}).");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AlarmReportLogger] ERROR linking/creating batch: {ex.Message}");
                activeBatchId = null;
            }
        }

        private void CompleteActiveBatch()
        {
            if (activeBatchId == null) return;
            
            try
            {
                dataAccess.ConnectionString = GetConnectionStringWithDb();
                string updateQuery = $"UPDATE `batches` " +
                                     $"SET `status` = 'Completed', `end_time` = '{DateTime.Now:yyyy-MM-dd HH:mm:ss}' " +
                                     $"WHERE `id` = {activeBatchId.Value}";
                dataAccess.ExecuteNonQuery(updateQuery);
                System.Diagnostics.Debug.WriteLine($"[AlarmReportLogger] Completed batch ID {activeBatchId.Value} successfully.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AlarmReportLogger] ERROR completing batch: {ex.Message}");
            }
            finally
            {
                activeBatchId = null;
            }
        }

        /// <summary>
        /// Create the alarmreport table with all 17 register columns + metadata columns.
        /// Columns: ID (auto), DateTime, DeviceName, QuyTrinh, CongDoanMay,
        /// + 17 time/temperature registers.
        /// </summary>
        public bool CreateTableIfNotExists()
        {
            try
            {
                dataAccess.ConnectionString = GetConnectionStringWithDb();

                var sb = new StringBuilder();
                sb.Append($"CREATE TABLE IF NOT EXISTS `{TableName}` (");
                sb.Append("`ID` INT AUTO_INCREMENT PRIMARY KEY, ");
                sb.Append("`DateTime` DATETIME NOT NULL, ");
                sb.Append("`DeviceName` VARCHAR(100) NOT NULL, ");
                sb.Append("`QuyTrinh` INT NOT NULL DEFAULT 0, ");
                sb.Append("`CongDoanMay` INT NOT NULL DEFAULT 0, ");
                sb.Append("`batchId` INT NULL DEFAULT NULL");

                // Add columns for each configured register
                foreach (var item in logItems)
                {
                    // Skip dedicated columns (managed by State Machine, not from Collection)
                    if (string.Equals(item.Alias, "CongDoanMay", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(item.Alias, "QuyTrinh", StringComparison.OrdinalIgnoreCase))
                        continue;

                    sb.Append($", `{item.Alias}` VARCHAR(200) NOT NULL DEFAULT '0'");
                }

                sb.Append(")");

                return dataAccess.ExecuteNonQuery(sb.ToString()) >= 0;
            }
            catch (Exception ex) 
            { 
                System.Diagnostics.Debug.WriteLine($"[AlarmReportLogger] CreateTable ERROR: {ex.Message}");
                return false; 
            }
        }

        /// <summary>
        /// Insert one alarmreport row with current tag values.
        /// </summary>
        private bool InsertAlarmReport()
        {
            try
            {
                if (!CreateDatabaseIfNotExists()) return false;
                if (!CreateTableIfNotExists()) return false;

                dataAccess.ConnectionString = GetConnectionStringWithDb();

                var fieldBuilder = new StringBuilder();
                var valueBuilder = new StringBuilder();

                foreach (var item in logItems)
                {
                    // Skip dedicated columns (managed by State Machine, not from Collection)
                    if (string.Equals(item.Alias, "CongDoanMay", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(item.Alias, "QuyTrinh", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var value = item.Tag?.Value ?? "0";
                    fieldBuilder.Append($", `{item.Alias}`");
                    valueBuilder.Append($", '{value}'");
                }

                string batchIdValue = activeBatchId.HasValue ? activeBatchId.Value.ToString() : "NULL";

                var query = $"INSERT INTO `{TableName}` " +
                    $"(`DateTime`, `DeviceName`, `QuyTrinh`, `CongDoanMay`, `batchId`{fieldBuilder}) " +
                    $"VALUES ('{DateTime.Now:yyyy-MM-dd HH:mm:ss}', '{deviceName}', {currentQuyTrinh}, {currentCongDoan}, {batchIdValue}{valueBuilder})";

                return dataAccess.ExecuteNonQuery(query) >= 0;
            }
            catch (Exception ex) 
            { 
                System.Diagnostics.Debug.WriteLine($"[AlarmReportLogger] Insert ERROR: {ex.Message}");
                return false; 
            }
        }

        /// <summary>
        /// Query DB for the maximum QuyTrinh value so we can increment for a new batch.
        /// </summary>
        private int GetMaxQuyTrinhFromDb()
        {
            try
            {
                if (!CreateDatabaseIfNotExists()) return 0;
                if (!CreateTableIfNotExists()) return 0;

                dataAccess.ConnectionString = GetConnectionStringWithDb();

                var query = $"SELECT IFNULL(MAX(`QuyTrinh`), 0) FROM `{TableName}` WHERE `DeviceName` = '{deviceName}'";
                var result = dataAccess.ExecuteScalarQuery(query);

                if (result != null && result != DBNull.Value)
                {
                    return Convert.ToInt32(result);
                }
                return 0;
            }
            catch (Exception ex) 
            { 
                System.Diagnostics.Debug.WriteLine($"[AlarmReportLogger] GetMaxQuyTrinh ERROR: {ex.Message}");
                return 0; 
            }
        }

        /// <summary>
        /// Insert an INFO milestone event into the realtime_alarms table.
        /// </summary>
        private bool InsertRealtimeInfoEvent(string stageName, string message)
        {
            try
            {
                if (!CreateDatabaseIfNotExists()) return false;

                dataAccess.ConnectionString = GetConnectionStringWithDb();

                // Ensure the realtime_alarms table and all of its new columns exist before writing.
                // We'll execute an ALTER statement to add new columns if they are missing.
                string tblName = "realtime_alarms";
                string createTableSql = $"CREATE TABLE IF NOT EXISTS `{tblName}` (" +
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
                    "`Severity` VARCHAR(50) NOT NULL DEFAULT 'ALARM', " +
                    "`restore_time` DATETIME NULL DEFAULT NULL" +
                    ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";
                dataAccess.ExecuteNonQuery(createTableSql);

                // Add columns just in case the table existed without them
                string[] checkCols = { "QuyTrinh", "CongDoan", "batchId", "Severity", "restore_time" };
                string[] alterSqls = {
                    $"ALTER TABLE `{tblName}` ADD COLUMN `QuyTrinh` INT NOT NULL DEFAULT 0",
                    $"ALTER TABLE `{tblName}` ADD COLUMN `CongDoan` VARCHAR(100) NOT NULL DEFAULT ''",
                    $"ALTER TABLE `{tblName}` ADD COLUMN `batchId` INT NULL DEFAULT NULL",
                    $"ALTER TABLE `{tblName}` ADD COLUMN `Severity` VARCHAR(50) NOT NULL DEFAULT 'ALARM'",
                    $"ALTER TABLE `{tblName}` ADD COLUMN `restore_time` DATETIME NULL DEFAULT NULL"
                };

                for (int i = 0; i < checkCols.Length; i++)
                {
                    try
                    {
                        string checkQuery = $"SHOW COLUMNS FROM `{tblName}` LIKE '{checkCols[i]}'";
                        var res = dataAccess.ExecuteScalarQuery(checkQuery);
                        if (res == null || res == DBNull.Value)
                        {
                            dataAccess.ExecuteNonQuery(alterSqls[i]);
                        }
                    }
                    catch { }
                }

                // Check for duplicate INFO log within the same stage (CongDoan) of the same batch/process
                string checkDuplicateQuery;
                if (activeBatchId.HasValue)
                {
                    checkDuplicateQuery = $"SELECT COUNT(*) FROM `{tblName}` " +
                                          $"WHERE `batchId` = {activeBatchId.Value} AND `CongDoan` = '{stageName}' AND `Severity` = 'INFO'";
                }
                else
                {
                    checkDuplicateQuery = $"SELECT COUNT(*) FROM `{tblName}` " +
                                          $"WHERE `DeviceName` = '{deviceName}' AND `QuyTrinh` = {currentQuyTrinh} AND `CongDoan` = '{stageName}' AND `Severity` = 'INFO'";
                }

                var countObj = dataAccess.ExecuteScalarQuery(checkDuplicateQuery);
                if (countObj != null && countObj != DBNull.Value)
                {
                    int count = Convert.ToInt32(countObj);
                    if (count > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"[AlarmReportLogger] INFO event for stage '{stageName}' already logged in this batch. Skipping duplicate.");
                        return true;
                    }
                }

                string batchIdValue = activeBatchId.HasValue ? activeBatchId.Value.ToString() : "NULL";

                var query = $"INSERT INTO `{tblName}` " +
                    $"(`DateTime`, `DeviceName`, `TagName`, `Value`, `Threshold`, `Operator`, `Message`, `QuyTrinh`, `CongDoan`, `batchId`, `Severity`) " +
                    $"VALUES (" +
                    $"'{DateTime.Now:yyyy-MM-dd HH:mm:ss}', " +
                    $"'{deviceName}', " +
                    $"'System', " +
                    $"0, " +
                    $"0, " +
                    $"'=', " +
                    $"'{message}', " +
                    $"{currentQuyTrinh}, " +
                    $"'{stageName}', " +
                    $"{batchIdValue}, " +
                    $"'INFO')";

                return dataAccess.ExecuteNonQuery(query) >= 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AlarmReportLogger] InsertRealtimeInfoEvent ERROR: {ex.Message}");
                return false;
            }
        }

        #endregion
    }
}
