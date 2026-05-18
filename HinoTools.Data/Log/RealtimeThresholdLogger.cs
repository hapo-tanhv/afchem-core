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

        /// <summary>Ngưỡng cảnh báo (ví dụ: 50 cho nhiệt độ)</summary>
        public double Threshold { get; set; }

        /// <summary>Toán tử so sánh: ">", "<", "="</summary>
        public string Operator { get; set; } = ">";

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
        public string TableName { get; set; } = "realtime_alarms";

        [Category("Hino Settings")]
        [Description("Scanning interval in milliseconds (default: 3000ms = 3s).")]
        public int ScanInterval { get; set; } = 3000;

        private string[] _collection;

        [Category("Hino Settings")]
        [Description("Format: TagName;Alias;Threshold;Operator. " +
                      "Operator: > | < | = (default: >). " +
                      "Example: AFChemPLC.NhietDoMay;NhietDoMay;50;>")]
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

            thresholdItems = GetThresholdItems().ToList();
            if (thresholdItems.Count == 0) return;

            deviceName = ExtractDeviceName();

            dataAccess = new DataAccess();
            tmrScan = new System.Timers.Timer();
            tmrScan.Interval = ScanInterval;
            tmrScan.AutoReset = false;
            tmrScan.Elapsed += (sender, e) => ScanAndLog();
            tmrScan.Start();
        }

        /// <summary>
        /// Parse Collection config into ThresholdItem list.
        /// Format: "TagName;Alias;Threshold;Operator"
        /// If Operator is omitted, defaults to ">".
        /// </summary>
        private IEnumerable<ThresholdItem> GetThresholdItems()
        {
            if (Collection == null) yield break;

            foreach (var item in Collection)
            {
                var parts = item.Split(';');
                if (parts.Length < 3) continue; // Minimum: TagName;Alias;Threshold

                double threshold;
                if (!double.TryParse(parts[2], out threshold)) continue;

                var op = parts.Length >= 4 ? parts[3].Trim() : ">";
                if (op != ">" && op != "<" && op != "=")
                    op = ">"; // Fallback to default

                yield return new ThresholdItem()
                {
                    TagName = parts[0],
                    Tag = this.driver?.GetTagByName(parts[0]),
                    Alias = parts[1],
                    Threshold = threshold,
                    Operator = op
                };
            }
        }

        /// <summary>
        /// Extract device name prefix from first tag (e.g. "AFChemPLC.NhietDoMay" -> "AFChemPLC").
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

        /// <summary>
        /// Core scan method called every 3 seconds.
        /// Reads all configured tags and checks thresholds.
        /// </summary>
        private void ScanAndLog()
        {
            try
            {
                tmrScan.Stop();

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

                    bool isViolating = EvaluateThreshold(currentValue, item.Operator, item.Threshold);
                    bool wasAlarming;
                    alarmActiveStates.TryGetValue(item.Alias, out wasAlarming);

                    if (isViolating && !wasAlarming)
                    {
                        // Rising edge: first violation -> INSERT and mark as alarming
                        InsertRealtimeAlarm(item, currentValue);
                        alarmActiveStates[item.Alias] = true;
                    }
                    else if (!isViolating && wasAlarming)
                    {
                        // Falling edge: value returned to safe -> reset state
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
        /// Columns: ID, DateTime, DeviceName, TagName, Value, Threshold, Operator, Message.
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
                    "`Message` VARCHAR(500) NOT NULL DEFAULT ''" +
                    ")";

                return dataAccess.ExecuteNonQuery(query) >= 0;
            }
            catch { return false; }
        }

        /// <summary>
        /// Insert a threshold violation record.
        /// </summary>
        private bool InsertRealtimeAlarm(ThresholdItem item, double currentValue)
        {
            try
            {
                if (!CreateDatabaseIfNotExists()) return false;
                if (!CreateTableIfNotExists()) return false;

                dataAccess.ConnectionString = GetConnectionStringWithDb();

                var valStr = currentValue.ToString(CultureInfo.InvariantCulture);
                var threshStr = item.Threshold.ToString(CultureInfo.InvariantCulture);
                var message = $"{item.Alias} = {valStr} {item.Operator} {threshStr}";

                var query = $"INSERT INTO `{TableName}` " +
                    $"(`DateTime`, `DeviceName`, `TagName`, `Value`, `Threshold`, `Operator`, `Message`) " +
                    $"VALUES (" +
                    $"'{DateTime.Now:yyyy-MM-dd HH:mm:ss}', " +
                    $"'{deviceName}', " +
                    $"'{item.Alias}', " +
                    $"{valStr}, " +
                    $"{threshStr}, " +
                    $"'{item.Operator}', " +
                    $"'{message}')";

                return dataAccess.ExecuteNonQuery(query) >= 0;
            }
            catch { return false; }
        }

        #endregion
    }
}
