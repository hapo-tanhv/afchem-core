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

        #endregion

        #region PROPERTIES

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
        public string Password { get; set; } = "100100";

        [Category("Hino Settings")]
        public string DatabaseName { get; set; } = "scada";

        [Category("Hino Settings")]
        public string TableName { get; set; } = "alarmreport";

        [Category("Hino Settings")]
        [Description("Polling interval in milliseconds (default: 30000ms = 30s).")]
        public int PollingInterval { get; set; } = 30000;

        private string[] _collection;

        [Category("Hino Settings")]
        [Description("Format: TagName;Alias (17 registers + CongDoanMay). " +
                      "Example: AFChemPLC.ThoiGianCapLieu;ThoiGianCapLieu")]
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

            // Extract DeviceName from the first tag's full name (e.g. "AFChemPLC.ThoiGianCapLieu" -> "AFChemPLC")
            deviceName = ExtractDeviceName();

            dataAccess = new DataAccess();
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
        /// Extract the device name prefix from the first tag in Collection.
        /// E.g. "AFChemPLC.ThoiGianCapLieu" -> "AFChemPLC"
        /// </summary>
        private string ExtractDeviceName()
        {
            if (Collection == null || Collection.Length == 0)
                return "Unknown";

            var firstTag = Collection[0].Split(';')[0]; // e.g. "AFChemPLC.ThoiGianCapLieu"
            var dotIndex = firstTag.IndexOf('.');
            return dotIndex > 0 ? firstTag.Substring(0, dotIndex) : firstTag;
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
                    if (hasThoiGianCapLieuStarted && thoiGianCapLieu == 0) currentCongDoan = 2;
                }
                
                if (currentCongDoan == 2)
                {
                    if (thoiGianTron1 > 0) hasThoiGianTron1Started = true;
                    if (hasThoiGianTron1Started && thoiGianTron1 == 0) currentCongDoan = 3;
                }

                if (currentCongDoan == 3)
                {
                    if (thoiGianHutXa > 0) hasThoiGianHutXaStarted = true;
                    if (hasThoiGianHutXaStarted && thoiGianHutXa == 0) currentCongDoan = 4;
                }

                if (currentCongDoan == 4)
                {
                    if (thoiGianTron2 > 0) hasThoiGianTron2Started = true;
                    if (hasThoiGianTron2Started && thoiGianTron2 == 0) currentCongDoan = 5;
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
                    }
                }

                // Logging decision
                if (currentCongDoan > 0)
                {
                    InsertAlarmReport();
                }
                else if (previousCongDoan == 5 && currentCongDoan == 0)
                {
                    // Batch ended in this tick AND new batch didn't start yet.
                    // Log the final zero values with CongDoan = 5.
                    currentCongDoan = 5;
                    InsertAlarmReport();
                    currentCongDoan = 0;
                }

                // Dynamic Polling Interval:
                // Fast poll (1s) when Idle to catch START immediately.
                // Slow poll (30s) during batch to log continuously.
                if (currentCongDoan == 0)
                {
                    tmrLog.Interval = 1000; // 1 second
                }
                else
                {
                    tmrLog.Interval = PollingInterval > 0 ? PollingInterval : 30000; // e.g. 30 seconds
                }

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
                sb.Append("`CongDoanMay` INT NOT NULL DEFAULT 0");

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

                var query = $"INSERT INTO `{TableName}` " +
                    $"(`DateTime`, `DeviceName`, `QuyTrinh`, `CongDoanMay`{fieldBuilder}) " +
                    $"VALUES ('{DateTime.Now:yyyy-MM-dd HH:mm:ss}', '{deviceName}', {currentQuyTrinh}, {currentCongDoan}{valueBuilder})";

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

        #endregion
    }
}
