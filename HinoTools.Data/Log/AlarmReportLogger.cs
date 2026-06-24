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

        // Previous values for stage duration alarm falling edge check
        private double prevCapLieu = 0;
        private double prevTron1 = 0;
        private double prevXaDay = 0;
        private double prevRungXaDay = 0;
        private double prevHutXaDay = 0;
        private double prevTron2 = 0;
        private double prevXaHang = 0;
        private double prevRungXaHang = 0;

        // Tracking for stop/pause
        private DateTime? stopStartTime = null;

        // Tag references (resolved once at startup)
        private List<LogItem> logItems;

        // Batches and API fields
        private HinoTools.Data.Http.BatchesHttpServer httpServer;
        private HinoTools.Data.Http.WebhookHttpServer webhookServer;
        private int? activeBatchId = null;
        private int? activeRunId = null;

        private DateTime lastAlarmReportTime = DateTime.MinValue;
        private double[] lastSetpoints = new double[8] { -1, -1, -1, -1, -1, -1, -1, -1 };

        #endregion

        #region PROPERTIES

        public int CurrentCongDoan => currentCongDoan;
        public int CurrentQuyTrinh => currentQuyTrinh;
        public int? ActiveBatchId => activeBatchId;
        public int? ActiveRunId => activeRunId;

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
        [Description("HTTP Server API Port (Disabled, was default 5500).")]
        public int HttpPort { get; set; } = 5500;

        [Category("Hino Settings")]
        [Description("Webhook HTTP Server API Port (default: 5605).")]
        public int WebhookPort { get; set; } = 5605;

        [Category("Hino Settings")]
        [Description("Webhook Secret Security Token.")]
        public string WebhookToken { get; set; } = "wh_tok_2f8d9b1e4c7a6e5b3d2c1f0a9e8d7c6b";

        [Category("Hino Settings")]
        [Description("Timeout in seconds before a stopped/paused batch in stage 1-4 is marked as Error (default: 7200s = 2h).")]
        public int StopTimeout { get; set; } = 7200;

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
            AddRunIdColumnIfNeeded(TableName);
            ResolveOrphanPauseRecords();

            // HTTP API Server (Disabled/Cleared)
            System.Diagnostics.Debug.WriteLine("[AlarmReportLogger] HTTP Server (port 5500) is disabled.");

            // Start Webhook HTTP Server
            try
            {
                webhookServer = new HinoTools.Data.Http.WebhookHttpServer(GetConnectionStringWithDb(), WebhookPort, WebhookToken);
                webhookServer.Start();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AlarmReportLogger] Webhook HTTP Server start FAILED: {ex.Message}");
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

                // Sync active run and batch from the database dynamically to prevent out-of-sync state
                try
                {
                    dataAccess.ConnectionString = GetConnectionStringWithDb();
                    string activeQuery = "SELECT r.id, r.batch_id FROM `runs` r " +
                                         "JOIN `batches` b ON r.batch_id = b.id " +
                                         $"WHERE b.device_name = '{deviceName}' AND r.status = 'Active' " +
                                         "ORDER BY r.id DESC LIMIT 1";
                    var activeDt = dataAccess.ExecuteQuery(activeQuery);
                    if (activeDt != null && activeDt.Rows.Count > 0)
                    {
                        int dbRunId = Convert.ToInt32(activeDt.Rows[0]["id"]);
                        int dbBatchId = Convert.ToInt32(activeDt.Rows[0]["batch_id"]);

                        // If the database active run ID has changed, reset the in-memory state machine
                        if (activeRunId != dbRunId)
                        {
                            System.Diagnostics.Debug.WriteLine($"[AlarmReportLogger] New active run detected in DB: {dbRunId} (old in-memory: {activeRunId}). Resetting state machine to align.");
                            activeRunId = dbRunId;
                            activeBatchId = dbBatchId;
                            currentCongDoan = 1;
                            ResetFlags();
                            lastAlarmReportTime = DateTime.MinValue; // Trigger instant logging for the new run
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[AlarmReportLogger] Sync active run ERROR: {ex.Message}");
                }

                // Check and update HMI/PLC setpoints in the runs table
                CheckAndUpdateSetpoints();

                double thoiGianCapLieu = GetTagValueByAlias("ThoiGianCapLieu");
                double thoiGianTron1 = GetTagValueByAlias("ThoiGianTron1");
                double thoiGianHutXa = GetTagValueByAlias("ThoiGianHutXaDay");
                double thoiGianTron2 = GetTagValueByAlias("ThoiGianTron2");
                double thoiGianRungXaHang = GetTagValueByAlias("ThoiGianRungXaHang");
                double thoiGianXaHang = GetTagValueByAlias("ThoiGianXaHang");

                double stopValue = GetSystemTagValue("Stop");
                double runValue = GetSystemTagValue("Run");
                bool isStopped = (stopValue == 1);

                System.Diagnostics.Debug.WriteLine(string.Format("[AlarmReportLogger] Poll: CongDoan={0}, QuyTrinh={1}, ActiveRun={2}, ActiveBatch={3}, Stop={4}, Run={5}", 
                    currentCongDoan, currentQuyTrinh, activeRunId, activeBatchId, stopValue, runValue));

                int previousCongDoan = currentCongDoan;

                double thoiGianXaDay = GetTagValueByAlias("ThoiGianXaDay");
                double thoiGianRungXaDay = GetTagValueByAlias("ThoiGianRungXaDay");

                // Falling edge detection for stage duration alarms
                if (activeRunId != null)
                {
                    if (prevCapLieu > 0 && thoiGianCapLieu == 0) CheckAndLogStageDurationAlarm("T001", prevCapLieu);
                    if (prevTron1 > 0 && thoiGianTron1 == 0) CheckAndLogStageDurationAlarm("T002", prevTron1);
                    if (prevXaDay > 0 && thoiGianXaDay == 0) CheckAndLogStageDurationAlarm("T003", prevXaDay);
                    if (prevRungXaDay > 0 && thoiGianRungXaDay == 0) CheckAndLogStageDurationAlarm("T004", prevRungXaDay);
                    if (prevHutXaDay > 0 && thoiGianHutXa == 0) CheckAndLogStageDurationAlarm("T005", prevHutXaDay);
                    if (prevTron2 > 0 && thoiGianTron2 == 0) CheckAndLogStageDurationAlarm("T006", prevTron2);
                    if (prevXaHang > 0 && thoiGianXaHang == 0) CheckAndLogStageDurationAlarm("T007", prevXaHang);
                    if (prevRungXaHang > 0 && thoiGianRungXaHang == 0) CheckAndLogStageDurationAlarm("T008", prevRungXaHang);
                }

                // 1. Check for Reset event (Stop = 1 and active stage timer is reset to 0, or all stage timers are 0)
                bool isReset = false;
                if (currentCongDoan > 0 && currentCongDoan < 5 && isStopped)
                {
                    bool allTimersZero = thoiGianCapLieu == 0 &&
                                         thoiGianTron1 == 0 &&
                                         thoiGianXaDay == 0 &&
                                         thoiGianRungXaDay == 0 &&
                                         thoiGianHutXa == 0 &&
                                         thoiGianTron2 == 0 &&
                                         thoiGianXaHang == 0 &&
                                         thoiGianRungXaHang == 0;

                    if (allTimersZero)
                        isReset = true;
                    else if (currentCongDoan == 1 && hasThoiGianCapLieuStarted && thoiGianCapLieu == 0)
                        isReset = true;
                    else if (currentCongDoan == 2 && hasThoiGianTron1Started && thoiGianTron1 == 0)
                        isReset = true;
                    else if (currentCongDoan == 3 && thoiGianXaDay == 0 && thoiGianRungXaDay == 0 && thoiGianHutXa == 0)
                        isReset = true;
                    else if (currentCongDoan == 4 && hasThoiGianTron2Started && thoiGianTron2 == 0)
                        isReset = true;
                }

                // 2. State Machine logic
                if (currentCongDoan > 0)
                {
                    if (isReset)
                    {
                        System.Diagnostics.Debug.WriteLine(string.Format("[AlarmReportLogger] Reset detected in stage {0}. Aborting run.", currentCongDoan));
                        
                        // Capture final state in alarmreport log
                        InsertAlarmReport();
                        
                        UpdatePauseRecord(DateTime.Now); // Close the pause event if any
                        
                        // Fail the run
                        FailActiveBatch();
                        
                        currentCongDoan = 0; // Return to Idle
                        stopStartTime = null;
                    }
                    else if (isStopped)
                    {
                        if (currentCongDoan == 5)
                        {
                            // In stage 5, Stop = 1 means complete run normally immediately
                            currentCongDoan = 0; // Return to Idle
                            InsertRealtimeInfoEvent("IDLE", "Xả liệu hoàn tất (Dừng máy)");
                            CompleteActiveBatch();
                            stopStartTime = null;
                        }
                        else
                        {
                            bool isPauseActive = thoiGianCapLieu > 0 ||
                                                 thoiGianTron1 > 0 ||
                                                 thoiGianXaDay > 0 ||
                                                 thoiGianRungXaDay > 0 ||
                                                 thoiGianHutXa > 0 ||
                                                 thoiGianTron2 > 0 ||
                                                 thoiGianXaHang > 0 ||
                                                 thoiGianRungXaHang > 0;

                            if (isPauseActive)
                            {
                                // Reset started flags when stopped/paused to avoid false transitions on resume
                                ResetFlags();

                                // Track timeout for long stopped state (e.g. 2 hours)
                                if (stopStartTime == null)
                                {
                                    stopStartTime = DateTime.Now;
                                    InsertPauseRecord(); // Log pause event!
                                }
                                else
                                {
                                    double elapsed = (DateTime.Now - stopStartTime.Value).TotalSeconds;
                                    if (elapsed >= StopTimeout)
                                    {
                                        System.Diagnostics.Debug.WriteLine(string.Format("[AlarmReportLogger] Run stopped for {0}s. Auto-cleaning and marking as Error.", elapsed));
                                        InsertAlarmReport();
                                        UpdatePauseRecord(DateTime.Now); // Close the pause event
                                        FailActiveBatch();
                                        currentCongDoan = 0; // Return to Idle
                                        stopStartTime = null;
                                    }
                                }
                            }
                            else
                            {
                                // Stop = 1 but all registers are 0. This is not a temporary pause.
                                // Close the pause record if it was active.
                                if (stopStartTime != null)
                                {
                                    System.Diagnostics.Debug.WriteLine("[AlarmReportLogger] Stop = 1 and all registers are 0. Closing existing pause record.");
                                    UpdatePauseRecord(DateTime.Now);
                                    stopStartTime = null;
                                }
                            }
                        }
                    }
                    else
                    {
                        // Resumed / Running normally
                        if (stopStartTime != null)
                        {
                            System.Diagnostics.Debug.WriteLine("[AlarmReportLogger] Machine resumed. Resetting timeout tracker.");
                            UpdatePauseRecord(DateTime.Now); // Close the pause event
                            stopStartTime = null;
                        }

                        // Check if a new run has started mid-way (Self-healing for un-notified resets)
                        if (currentCongDoan > 1 && thoiGianCapLieu > 0)
                        {
                            System.Diagnostics.Debug.WriteLine(string.Format("[AlarmReportLogger] New run start detected while in stage {0}. Marking previous run as Error.", currentCongDoan));
                            FailActiveBatch();
                            
                            // Initialize new run
                            currentQuyTrinh = GetMaxQuyTrinhFromDb() + 1;
                            currentCongDoan = 1;
                            ResetFlags();
                            hasThoiGianCapLieuStarted = true;
                            LinkOrCreateActiveBatch();
                            InsertRealtimeInfoEvent("T001", "Bắt đầu cấp liệu");
                            lastAlarmReportTime = DateTime.MinValue;
                        }
                        else
                        {
                            // Double-Lock Stage Transitions (Timer == 0 AND Next Timer > 0)
                            if (currentCongDoan == 1)
                            {
                                if (thoiGianCapLieu > 0) hasThoiGianCapLieuStarted = true;
                                if (hasThoiGianCapLieuStarted && thoiGianCapLieu == 0 && thoiGianTron1 > 0)
                                {
                                    currentCongDoan = 2;
                                    InsertRealtimeInfoEvent("T002", "Bắt đầu trộn lần 1");
                                }
                            }
                            
                            else if (currentCongDoan == 2)
                            {
                                if (thoiGianTron1 > 0) hasThoiGianTron1Started = true;
                                if (hasThoiGianTron1Started && thoiGianTron1 == 0 && (thoiGianXaDay > 0 || thoiGianRungXaDay > 0 || thoiGianHutXa > 0))
                                {
                                    currentCongDoan = 3;
                                    InsertRealtimeInfoEvent("T003", "Bắt đầu xả đáy");
                                }
                            }

                            else if (currentCongDoan == 3)
                            {
                                if (thoiGianRungXaDay > 0)
                                {
                                    InsertRealtimeInfoEvent("T004", "Bắt đầu rung xả đáy");
                                }
                                if (thoiGianHutXa > 0)
                                {
                                    InsertRealtimeInfoEvent("T005", "Bắt đầu hút xả đáy");
                                }

                                if (thoiGianHutXa > 0) hasThoiGianHutXaStarted = true;
                                if (hasThoiGianHutXaStarted && thoiGianHutXa == 0 && thoiGianTron2 > 0)
                                {
                                    currentCongDoan = 4;
                                    InsertRealtimeInfoEvent("T006", "Bắt đầu trộn lần 2");
                                }
                            }

                            else if (currentCongDoan == 4)
                            {
                                if (thoiGianTron2 > 0) hasThoiGianTron2Started = true;
                                if (hasThoiGianTron2Started && thoiGianTron2 == 0 && (thoiGianXaHang > 0 || thoiGianRungXaHang > 0))
                                {
                                    currentCongDoan = 5;
                                    InsertRealtimeInfoEvent("T007", "Bắt đầu xả hàng");
                                }
                            }

                            else if (currentCongDoan == 5)
                            {
                                if (thoiGianRungXaHang > 0)
                                {
                                    InsertRealtimeInfoEvent("T008", "Bắt đầu rung xả hàng");
                                }

                                if (thoiGianRungXaHang > 0) hasThoiGianRungXaHangStarted = true;
                                if (thoiGianXaHang > 0) hasThoiGianXaHangStarted = true;

                                if (hasThoiGianRungXaHangStarted && thoiGianRungXaHang == 0) isThoiGianRungXaHangFinished = true;
                                if (hasThoiGianXaHangStarted && thoiGianXaHang == 0) isThoiGianXaHangFinished = true;

                                bool rungFinished = !hasThoiGianRungXaHangStarted || isThoiGianRungXaHangFinished;
                                bool xaFinished = !hasThoiGianXaHangStarted || isThoiGianXaHangFinished;

                                if (rungFinished && xaFinished)
                                {
                                    currentCongDoan = 0; // Return to Idle
                                    InsertRealtimeInfoEvent("IDLE", "Xả liệu hoàn tất");
                                    CompleteActiveBatch();
                                }
                            }
                        }
                    }
                }

                if (currentCongDoan == 0) // Idle
                {
                    if (thoiGianCapLieu > 0 && !isStopped)
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

                // Update previous values for next polling cycle
                prevCapLieu = thoiGianCapLieu;
                prevTron1 = thoiGianTron1;
                prevXaDay = thoiGianXaDay;
                prevRungXaDay = thoiGianRungXaDay;
                prevHutXaDay = thoiGianHutXa;
                prevTron2 = thoiGianTron2;
                prevXaHang = thoiGianXaHang;
                prevRungXaHang = thoiGianRungXaHang;

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

            prevCapLieu = 0;
            prevTron1 = 0;
            prevXaDay = 0;
            prevRungXaDay = 0;
            prevHutXaDay = 0;
            prevTron2 = 0;
            prevXaHang = 0;
            prevRungXaHang = 0;

            if (lastSetpoints != null)
            {
                for (int i = 0; i < lastSetpoints.Length; i++)
                {
                    lastSetpoints[i] = -1;
                }
            }
        }

        private string GetScaledValueString(string alias, string rawValue)
        {
            if (string.IsNullOrEmpty(rawValue)) return "0";

            if (string.Equals(alias, "CaiDatApSuat", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(alias, "ApSuat", StringComparison.OrdinalIgnoreCase))
            {
                if (double.TryParse(rawValue, out double val))
                {
                    return Math.Round(val / 100.0, 2).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
            }
            else if (string.Equals(alias, "DatNguongNhietDoMoiTruong", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(alias, "DatNguongDoAmMoiTruong", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(alias, "NhietDoMoiTruong", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(alias, "DoAmMoiTruong", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(alias, "NhietDoBonTronTren", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(alias, "NhietDoBonTronGiua", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(alias, "NhietDoBonTronDuoi", StringComparison.OrdinalIgnoreCase))
            {
                if (double.TryParse(rawValue, out double val))
                {
                    return Math.Round(val / 10.0, 2).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
            }
            return rawValue;
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

            if (string.Equals(alias, "CaiDatApSuat", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(alias, "ApSuat", StringComparison.OrdinalIgnoreCase))
            {
                val = Math.Round(val / 100.0, 2);
            }
            else if (string.Equals(alias, "DatNguongNhietDoMoiTruong", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(alias, "DatNguongDoAmMoiTruong", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(alias, "NhietDoMoiTruong", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(alias, "DoAmMoiTruong", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(alias, "NhietDoBonTronTren", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(alias, "NhietDoBonTronGiua", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(alias, "NhietDoBonTronDuoi", StringComparison.OrdinalIgnoreCase))
            {
                val = Math.Round(val / 10.0, 2);
            }

            return val;
        }

        private double GetSystemTagValue(string subTagName)
        {
            if (driver == null || Collection == null || Collection.Length == 0) return 0;

            // 1. Try to find it in Collection first
            var item = logItems.FirstOrDefault(x =>
                string.Equals(x.Alias, subTagName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.TagName, subTagName, StringComparison.OrdinalIgnoreCase) ||
                x.TagName.EndsWith("." + subTagName, StringComparison.OrdinalIgnoreCase));

            if (item != null)
            {
                if (item.Tag == null && driver != null)
                {
                    item.Tag = driver.GetTagByName(item.TagName);
                }
                if (item.Tag?.Value != null)
                {
                    double.TryParse(item.Tag.Value, out double val);
                    if (string.Equals(subTagName, "CaiDatApSuat", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(subTagName, "ApSuat", StringComparison.OrdinalIgnoreCase))
                    {
                        val = Math.Round(val / 100.0, 2);
                    }
                    else if (string.Equals(subTagName, "DatNguongNhietDoMoiTruong", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(subTagName, "DatNguongDoAmMoiTruong", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(subTagName, "NhietDoMoiTruong", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(subTagName, "DoAmMoiTruong", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(subTagName, "NhietDoBonTronTren", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(subTagName, "NhietDoBonTronGiua", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(subTagName, "NhietDoBonTronDuoi", StringComparison.OrdinalIgnoreCase))
                    {
                        val = Math.Round(val / 10.0, 2);
                    }
                    return val;
                }
            }

            // 2. If not found in Collection, build the full tag name from first tag's prefix
            var firstTag = Collection[0].Split(';')[0];
            var dotIndex = firstTag.IndexOf('.');
            string taskName = dotIndex > 0 ? firstTag.Substring(0, dotIndex) : "AFChemTX01";
            string fullTagName = string.Format("{0}.{1}", taskName, subTagName);

            var tag = driver.GetTagByName(fullTagName);
            if (tag?.Value != null)
            {
                double.TryParse(tag.Value, out double val);
                if (string.Equals(subTagName, "CaiDatApSuat", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(subTagName, "ApSuat", StringComparison.OrdinalIgnoreCase))
                {
                    val = Math.Round(val / 100.0, 2);
                }
                else if (string.Equals(subTagName, "DatNguongNhietDoMoiTruong", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(subTagName, "DatNguongDoAmMoiTruong", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(subTagName, "NhietDoMoiTruong", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(subTagName, "DoAmMoiTruong", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(subTagName, "NhietDoBonTronTren", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(subTagName, "NhietDoBonTronGiua", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(subTagName, "NhietDoBonTronDuoi", StringComparison.OrdinalIgnoreCase))
                {
                    val = Math.Round(val / 10.0, 2);
                }
                return val;
            }

            return 0;
        }

        private void CheckAndUpdateSetpoints()
        {
            if (activeRunId == null) return;

            try
            {
                // Read 8 setpoints from the driver
                double sp1 = GetSystemTagValue("ThoiGianCaiDatCapLieu");
                double sp2 = GetSystemTagValue("ThoiGianCaiDatTron1");
                double sp3 = GetSystemTagValue("ThoiGianCaiDatXaDay");
                double sp4 = GetSystemTagValue("ThoiGianCaiDatRungXaDay");
                double sp5 = GetSystemTagValue("ThoiGianCaiDatHutXaDayThem");
                double sp6 = GetSystemTagValue("ThoiGianCaiDatTron2");
                double sp7 = GetSystemTagValue("ThoiGianCaiDatXaHang");
                double sp8 = GetSystemTagValue("ThoiGianCaiDatRungXaHang");

                // Calculate effective setpoint for HutXaDayThem as (sp3 + sp5) because they run in parallel
                double sp5Effective = sp3 + sp5;

                // Check if any value has changed compared to lastSetpoints cache
                bool hasChanged = false;
                if (sp1 != lastSetpoints[0] || sp2 != lastSetpoints[1] || sp3 != lastSetpoints[2] ||
                    sp4 != lastSetpoints[3] || sp5Effective != lastSetpoints[4] || sp6 != lastSetpoints[5] ||
                    sp7 != lastSetpoints[6] || sp8 != lastSetpoints[7])
                {
                    hasChanged = true;
                }

                if (hasChanged)
                {
                    dataAccess.ConnectionString = GetConnectionStringWithDb();
                    string updateSql = "UPDATE `runs` SET " +
                                       $"`sp_thoi_gian_cap_lieu` = {(int)sp1}, " +
                                       $"`sp_thoi_gian_tron1` = {(int)sp2}, " +
                                       $"`sp_thoi_gian_xa_day` = {(int)sp3}, " +
                                       $"`sp_thoi_gian_rung_xa_day` = {(int)sp4}, " +
                                       $"`sp_thoi_gian_hut_xa_day_them` = {(int)sp5Effective}, " +
                                       $"`sp_thoi_gian_tron2` = {(int)sp6}, " +
                                       $"`sp_thoi_gian_xa_hang` = {(int)sp7}, " +
                                       $"`sp_thoi_gian_rung_xa_hang` = {(int)sp8} " +
                                       $"WHERE `id` = {activeRunId.Value}";
                    dataAccess.ExecuteNonQuery(updateSql);

                    // Update cached values
                    lastSetpoints[0] = sp1;
                    lastSetpoints[1] = sp2;
                    lastSetpoints[2] = sp3;
                    lastSetpoints[3] = sp4;
                    lastSetpoints[4] = sp5Effective;
                    lastSetpoints[5] = sp6;
                    lastSetpoints[6] = sp7;
                    lastSetpoints[7] = sp8;

                    System.Diagnostics.Debug.WriteLine($"[AlarmReportLogger] Updated setpoints for Run ID {activeRunId.Value} in database due to change or initialization.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AlarmReportLogger] ERROR checking/updating setpoints: {ex.Message}");
            }
        }

        private void FailActiveBatch()
        {
            if (activeRunId == null) return;

            try
            {
                dataAccess.ConnectionString = GetConnectionStringWithDb();
                string nowStr = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                // 1. Mark the active Run as 'Error'
                string failRunQuery = string.Format("UPDATE `runs` SET `status` = 'Error', `end_time` = '{0}' WHERE `id` = {1}", nowStr, activeRunId.Value);
                dataAccess.ExecuteNonQuery(failRunQuery);
                System.Diagnostics.Debug.WriteLine(string.Format("[AlarmReportLogger] Marked Run ID {0} as Error.", activeRunId.Value));

                // 2. Insert a compensating run for the same batch to ensure the batch target is met
                try
                {
                    string infoQuery = string.Format(
                        "SELECT b.name, b.total_runs, r.run_number, r.execution_order FROM `batches` b " +
                        "JOIN `runs` r ON r.batch_id = b.id " +
                        "WHERE b.id = {0} AND r.id = {1}", 
                        activeBatchId.Value, activeRunId.Value);
                    var infoDt = dataAccess.ExecuteQuery(infoQuery);
                    if (infoDt != null && infoDt.Rows.Count > 0)
                    {
                        string batchName = infoDt.Rows[0]["name"].ToString();
                        int currentTotalRuns = Convert.ToInt32(infoDt.Rows[0]["total_runs"]);
                        int failedRunNumber = Convert.ToInt32(infoDt.Rows[0]["run_number"]);
                        int failedRunExecutionOrder = Convert.ToInt32(infoDt.Rows[0]["execution_order"]);

                        // Check BOM details existence
                        string bomCheckQuery = string.Format("SELECT COUNT(*) FROM `run_info` WHERE `run_id` = {0}", activeRunId.Value);
                        var bomCountObj = dataAccess.ExecuteScalarQuery(bomCheckQuery);
                        int bomCount = bomCountObj != null ? Convert.ToInt32(bomCountObj) : 0;

                        if (bomCount == 0)
                        {
                            System.Diagnostics.Debug.WriteLine(string.Format("[AlarmReportLogger] Run ID {0} has no BOM details (test run). Skipping compensating run creation.", activeRunId.Value));
                        }
                        else
                        {
                            // Check retry limit (max 3 retries, meaning total 4 runs with the same run_number)
                            string retryCountQuery = string.Format("SELECT COUNT(*) FROM `runs` WHERE `batch_id` = {0} AND `run_number` = {1}", activeBatchId.Value, failedRunNumber);
                            var retryCountObj = dataAccess.ExecuteScalarQuery(retryCountQuery);
                            int retryCount = retryCountObj != null ? Convert.ToInt32(retryCountObj) : 0;

                            if (retryCount >= 4)
                            {
                                System.Diagnostics.Debug.WriteLine(string.Format("[AlarmReportLogger] Max retries reached (3 retries, total 4 runs) for run_number {0} in Batch ID {1}. Skipping compensating run creation.", failedRunNumber, activeBatchId.Value));
                            }
                            else
                            {
                                int newRunNumberForName = currentTotalRuns + 1;
                                string newRunName = string.Format("{0}-Me{1:D2}", batchName, newRunNumberForName);

                                // Increment total_runs in batches
                                string updateBatchRunsQuery = string.Format("UPDATE `batches` SET `total_runs` = {0} WHERE `id` = {1}", newRunNumberForName, activeBatchId.Value);
                                dataAccess.ExecuteNonQuery(updateBatchRunsQuery);

                                // Insert new compensating run as Pending (inheriting failed run's run_number and execution_order for priority)
                                string insertCompensatingRunQuery = string.Format(
                                    "INSERT INTO `runs` (`batch_id`, `run_number`, `name`, `status`, `execution_order`, `created_at`) VALUES ({0}, {1}, '{2}', 'Pending', {3}, NOW()); SELECT LAST_INSERT_ID();",
                                    activeBatchId.Value, failedRunNumber, newRunName, failedRunExecutionOrder);
                                var newRunIdObj = dataAccess.ExecuteScalarQuery(insertCompensatingRunQuery);
                                if (newRunIdObj != null && newRunIdObj != DBNull.Value)
                                {
                                    int newRunId = Convert.ToInt32(newRunIdObj);

                                    // Clone BOM (run_info) from the failed run to the new compensating run
                                    string cloneBomQuery = string.Format(
                                        "INSERT INTO `run_info` (`run_id`, `code`, `material_code`, `quantity`, `value`, `unit`, `batch_no`, `created_at`) " +
                                        "SELECT {0}, `code`, `material_code`, `quantity`, `value`, `unit`, `batch_no`, NOW() " +
                                        "FROM `run_info` WHERE `run_id` = {1}",
                                        newRunId, activeRunId.Value);
                                    dataAccess.ExecuteNonQuery(cloneBomQuery);
                                    System.Diagnostics.Debug.WriteLine(string.Format("[AlarmReportLogger] Created compensating Run '{0}' (ID: {1}, Priority RunNumber: {2}) and cloned BOM from failed Run ID {3}.", newRunName, newRunId, failedRunNumber, activeRunId.Value));
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(string.Format("[AlarmReportLogger] ERROR creating compensating run: {0}", ex.Message));
                }

                // 3. Check if all runs in the parent Batch are finished (no Pending or Active runs left)
                string checkRemainingQuery = string.Format("SELECT COUNT(*) FROM `runs` WHERE `batch_id` = {0} AND `status` IN ('Pending', 'Active')", activeBatchId.Value);
                var remainingObj = dataAccess.ExecuteScalarQuery(checkRemainingQuery);
                int remainingCount = remainingObj != null ? Convert.ToInt32(remainingObj) : 0;

                if (remainingCount == 0)
                {
                    // If no remaining runs, complete the parent Batch
                    string completeBatchQuery = string.Format("UPDATE `batches` SET `status` = 'Completed', `end_time` = '{0}' WHERE `id` = {1}", nowStr, activeBatchId.Value);
                    dataAccess.ExecuteNonQuery(completeBatchQuery);
                    System.Diagnostics.Debug.WriteLine(string.Format("[AlarmReportLogger] Completed Batch ID {0} since all its runs are completed/errored.", activeBatchId.Value));
                }

                // 4. Log a detailed alarm event to realtime_alarms
                string currentStageName = CurrentCongDoanName;
                string errorMessage = string.Format("Mẻ bị lỗi tại giai đoạn: {0} (Nhấn Stop/Lỗi hệ thống)", currentStageName);
                InsertRealtimeErrorEvent(CurrentCongDoanCode, errorMessage);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("[AlarmReportLogger] ERROR failing batch/run: {0}", ex.Message));
            }
            finally
            {
                activeRunId = null;
                activeBatchId = null;
            }
        }

        private bool InsertRealtimeErrorEvent(string stageName, string message)
        {
            try
            {
                if (!CreateDatabaseIfNotExists()) return false;

                dataAccess.ConnectionString = GetConnectionStringWithDb();

                string tblName = "realtime_alarms";
                string batchIdValue = activeBatchId.HasValue ? activeBatchId.Value.ToString() : "NULL";
                string runIdValue = activeRunId.HasValue ? activeRunId.Value.ToString() : "NULL";

                // Check for duplicate active error alarm (once per run)
                if (activeRunId.HasValue)
                {
                    string checkDupQuery = string.Format(
                        "SELECT COUNT(*) FROM `{0}` WHERE `runId` = {1} AND `TagName` = 'System' AND `CongDoan` = '{2}' AND `Severity` = 'ALARM'",
                        tblName, activeRunId.Value, stageName);
                    var dupObj = dataAccess.ExecuteScalarQuery(checkDupQuery);
                    if (dupObj != null && dupObj != DBNull.Value && Convert.ToInt32(dupObj) > 0)
                    {
                        System.Diagnostics.Debug.WriteLine(string.Format("[AlarmReportLogger] Error event for stage '{0}' already logged in this run. Skipping duplicate.", stageName));
                        return true;
                    }
                }

                var query = string.Format(
                    "INSERT INTO `{0}` (`DateTime`, `DeviceName`, `TagName`, `Value`, `Threshold`, `Operator`, `Message`, `QuyTrinh`, `CongDoan`, `batchId`, `runId`, `Severity`, `restore_time`) " +
                    "VALUES ('{1:yyyy-MM-dd HH:mm:ss}', '{2}', 'System', 1, 0, '=', '{3}', {4}, '{5}', {6}, {7}, 'ALARM', '{1:yyyy-MM-dd HH:mm:ss}')",
                    tblName, DateTime.Now, deviceName, message, currentQuyTrinh, stageName, batchIdValue, runIdValue);

                return dataAccess.ExecuteNonQuery(query) >= 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("[AlarmReportLogger] InsertRealtimeErrorEvent ERROR: {0}", ex.Message));
                return false;
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

        private void AddRunIdColumnIfNeeded(string tableName)
        {
            try
            {
                dataAccess.ConnectionString = GetConnectionStringWithDb();
                string checkQuery = $"SHOW COLUMNS FROM `{tableName}` LIKE 'runId'";
                var result = dataAccess.ExecuteScalarQuery(checkQuery);
                if (result == null || result == DBNull.Value)
                {
                    string alterQuery = $"ALTER TABLE `{tableName}` ADD COLUMN `runId` INT NULL DEFAULT NULL AFTER `batchId`";
                    dataAccess.ExecuteNonQuery(alterQuery);
                    System.Diagnostics.Debug.WriteLine($"[Migration] Added runId column to {tableName} successfully.");

                    // Migrate existing historical log records to map runId = run.id based on batchId
                    string migrateLogsSql = $"UPDATE `{tableName}` t " +
                                            "JOIN `runs` r ON t.batchId = r.batch_id " +
                                            "SET t.runId = r.id " +
                                            "WHERE t.runId IS NULL AND t.batchId IS NOT NULL";
                    dataAccess.ExecuteNonQuery(migrateLogsSql);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Migration] ERROR adding runId column to {tableName}: {ex.Message}");
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

                // Task 1.2: Check and add total_runs column to batches table
                string checkColQuery = "SHOW COLUMNS FROM `batches` LIKE 'total_runs'";
                var result = dataAccess.ExecuteScalarQuery(checkColQuery);
                if (result == null || result == DBNull.Value)
                {
                    string alterQuery = "ALTER TABLE `batches` ADD COLUMN `total_runs` INT NOT NULL DEFAULT 1 AFTER `status`";
                    dataAccess.ExecuteNonQuery(alterQuery);
                    System.Diagnostics.Debug.WriteLine("[Migration] Added total_runs column to batches table successfully.");
                }

                // Task 1.1 & 1.2: Create runs table
                string createRunsTableSql = "CREATE TABLE IF NOT EXISTS `runs` (" +
                                            "  `id` INT AUTO_INCREMENT PRIMARY KEY," +
                                            "  `batch_id` INT NOT NULL," +
                                            "  `run_number` INT NOT NULL," +
                                            "  `name` VARCHAR(150) NOT NULL UNIQUE," +
                                            "  `status` VARCHAR(50) NOT NULL DEFAULT 'Pending'," +
                                            "  `execution_order` INT NOT NULL DEFAULT 0," +
                                            "  `start_time` DATETIME NULL," +
                                            "  `end_time` DATETIME NULL," +
                                            "  `created_at` TIMESTAMP DEFAULT CURRENT_TIMESTAMP," +
                                            "  FOREIGN KEY (`batch_id`) REFERENCES `batches`(`id`) ON DELETE CASCADE," +
                                            "  INDEX `idx_runs_batch` (`batch_id`)," +
                                            "  INDEX `idx_runs_status` (`status`)" +
                                            ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";
                dataAccess.ExecuteNonQuery(createRunsTableSql);

                // Add setpoint columns to runs table if they don't exist
                string[] columnsToAdd = new string[]
                {
                    "`sp_thoi_gian_cap_lieu` INT NOT NULL DEFAULT 0 AFTER `status`",
                    "`sp_thoi_gian_tron1` INT NOT NULL DEFAULT 0 AFTER `sp_thoi_gian_cap_lieu`",
                    "`sp_thoi_gian_xa_day` INT NOT NULL DEFAULT 0 AFTER `sp_thoi_gian_tron1`",
                    "`sp_thoi_gian_rung_xa_day` INT NOT NULL DEFAULT 0 AFTER `sp_thoi_gian_xa_day`",
                    "`sp_thoi_gian_hut_xa_day_them` INT NOT NULL DEFAULT 0 AFTER `sp_thoi_gian_rung_xa_day`",
                    "`sp_thoi_gian_tron2` INT NOT NULL DEFAULT 0 AFTER `sp_thoi_gian_hut_xa_day_them`",
                    "`sp_thoi_gian_xa_hang` INT NOT NULL DEFAULT 0 AFTER `sp_thoi_gian_tron2`",
                    "`sp_thoi_gian_rung_xa_hang` INT NOT NULL DEFAULT 0 AFTER `sp_thoi_gian_xa_hang`"
                };

                string[] columnNames = new string[] {
                    "sp_thoi_gian_cap_lieu", "sp_thoi_gian_tron1", "sp_thoi_gian_xa_day",
                    "sp_thoi_gian_rung_xa_day", "sp_thoi_gian_hut_xa_day_them", "sp_thoi_gian_tron2",
                    "sp_thoi_gian_xa_hang", "sp_thoi_gian_rung_xa_hang"
                };

                for (int i = 0; i < columnNames.Length; i++)
                {
                    try
                    {
                        string checkColSql = $"SHOW COLUMNS FROM `runs` LIKE '{columnNames[i]}'";
                        var colResult = dataAccess.ExecuteScalarQuery(checkColSql);
                        if (colResult == null || colResult == DBNull.Value)
                        {
                            string alterSql = $"ALTER TABLE `runs` ADD COLUMN {columnsToAdd[i]}";
                            dataAccess.ExecuteNonQuery(alterSql);
                            System.Diagnostics.Debug.WriteLine($"[Migration] Added column {columnNames[i]} to runs table successfully.");
                        }
                    }
                    catch (Exception colEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Migration] ERROR adding column {columnNames[i]} to runs: {colEx.Message}");
                    }
                }

                // Check and add execution_order column to runs table if it doesn't exist
                try
                {
                    string checkColSql = "SHOW COLUMNS FROM `runs` LIKE 'execution_order'";
                    var colResult = dataAccess.ExecuteScalarQuery(checkColSql);
                    if (colResult == null || colResult == DBNull.Value)
                    {
                        string alterSql = "ALTER TABLE `runs` ADD COLUMN `execution_order` INT NOT NULL DEFAULT 0 AFTER `status`";
                        dataAccess.ExecuteNonQuery(alterSql);
                        System.Diagnostics.Debug.WriteLine("[Migration] Added column execution_order to runs table successfully.");

                        // Task 1.2: One-time backfill migration for historical runs
                        string backfillSql = "UPDATE `runs` SET `execution_order` = `id` WHERE `execution_order` = 0";
                        int backfillRows = dataAccess.ExecuteNonQuery(backfillSql);
                        System.Diagnostics.Debug.WriteLine($"[Migration] Backfilled {backfillRows} historical runs with execution_order.");
                    }
                }
                catch (Exception colEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[Migration] ERROR adding column execution_order to runs: {colEx.Message}");
                }

                // Task 1.4: Historical data migration (One-time check and execution)
                string migrateRunsSql = "INSERT INTO `runs` (`batch_id`, `run_number`, `name`, `status`, `start_time`, `end_time`, `created_at`) " +
                                        "SELECT b.id, 1, CONCAT(b.name, '-Me01'), b.status, b.start_time, b.end_time, b.created_at " +
                                        "FROM `batches` b " +
                                        "WHERE NOT EXISTS (SELECT 1 FROM `runs` r WHERE r.batch_id = b.id)";
                int rows = dataAccess.ExecuteNonQuery(migrateRunsSql);
                if (rows > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[Migration] Migrated {rows} historical batches to the new runs structure.");
                }
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

                // 0. Check if there is already an Active run for this device (prevents race condition and handles application restart)
                string activeQuery = "SELECT r.id, r.batch_id, b.start_time as batch_start_time FROM `runs` r " +
                                     "JOIN `batches` b ON r.batch_id = b.id " +
                                     $"WHERE b.device_name = '{deviceName}' AND r.status = 'Active' " +
                                     "ORDER BY r.id DESC LIMIT 1";
                var activeDt = dataAccess.ExecuteQuery(activeQuery);
                if (activeDt != null && activeDt.Rows.Count > 0)
                {
                    activeRunId = Convert.ToInt32(activeDt.Rows[0]["id"]);
                    activeBatchId = Convert.ToInt32(activeDt.Rows[0]["batch_id"]);
                    System.Diagnostics.Debug.WriteLine($"[AlarmReportLogger] Re-linked to already Active Run ID: {activeRunId.Value}, Batch ID: {activeBatchId.Value}");

                    // Check if parent batch's start_time is null, if so, update it
                    if (activeDt.Rows[0]["batch_start_time"] == DBNull.Value)
                    {
                        string updateBatchQuery = $"UPDATE `batches` " +
                                                  $"SET `start_time` = '{DateTime.Now:yyyy-MM-dd HH:mm:ss}' " +
                                                  $"WHERE `id` = {activeBatchId.Value}";
                        dataAccess.ExecuteNonQuery(updateBatchQuery);
                        System.Diagnostics.Debug.WriteLine($"[AlarmReportLogger] Updated start_time for active Batch ID {activeBatchId.Value} since it was null.");
                    }
                    return;
                }
                
                // 1. Find the oldest 'Pending' run for this device based on execution_order
                string findQuery = "SELECT r.id, r.batch_id, b.name as batch_name, b.status as batch_status, b.start_time as batch_start_time FROM `runs` r " +
                                   "JOIN `batches` b ON r.batch_id = b.id " +
                                   $"WHERE b.device_name = '{deviceName}' AND r.status = 'Pending' " +
                                   "ORDER BY r.execution_order ASC, r.id ASC LIMIT 1";
                
                var dt = dataAccess.ExecuteQuery(findQuery);
                if (dt != null && dt.Rows.Count > 0)
                {
                    int runId = Convert.ToInt32(dt.Rows[0]["id"]);
                    int batchId = Convert.ToInt32(dt.Rows[0]["batch_id"]);
                    string batchName = dt.Rows[0]["batch_name"].ToString();
                    string batchStatus = dt.Rows[0]["batch_status"].ToString();
                    
                    // Update this run to Active and set start_time
                    string updateRunQuery = $"UPDATE `runs` " +
                                            $"SET `status` = 'Active', `start_time` = '{DateTime.Now:yyyy-MM-dd HH:mm:ss}' " +
                                            $"WHERE `id` = {runId}";
                    dataAccess.ExecuteNonQuery(updateRunQuery);

                    // Update parent batch to Active if it is still Pending or start_time is null
                    bool shouldUpdateBatch = batchStatus == "Pending" || dt.Rows[0]["batch_start_time"] == DBNull.Value;
                    if (shouldUpdateBatch)
                    {
                        string updateBatchQuery = $"UPDATE `batches` " +
                                                  $"SET `status` = 'Active', `start_time` = '{DateTime.Now:yyyy-MM-dd HH:mm:ss}' " +
                                                  $"WHERE `id` = {batchId}";
                        dataAccess.ExecuteNonQuery(updateBatchQuery);
                        System.Diagnostics.Debug.WriteLine($"[AlarmReportLogger] Activated Batch '{batchName}' (ID: {batchId}) due to status Pending or start_time null.");
                    }
                    
                    activeRunId = runId;
                    activeBatchId = batchId;
                    System.Diagnostics.Debug.WriteLine($"[AlarmReportLogger] Priority linked Run ID: {runId} in Batch ID: {batchId} as Active.");
                }
                else
                {
                    // 2. If no Pending run exists, auto-generate a fallback/emergency batch & run (Self-healing)
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
                    
                    // Insert Batch and mark as Active immediately, then get its ID in the same query
                    string insertBatchQuery = $"INSERT INTO `batches` (`name`, `device_name`, `status`, `total_runs`, `start_time`, `created_at`) " +
                                              $"VALUES ('{fallbackBatchName}', '{deviceName}', 'Active', 1, '{DateTime.Now:yyyy-MM-dd HH:mm:ss}', NOW()); " +
                                              $"SELECT LAST_INSERT_ID();";
                    var lastBatchIdObj = dataAccess.ExecuteScalarQuery(insertBatchQuery);
                    if (lastBatchIdObj != null && lastBatchIdObj != DBNull.Value)
                    {
                        activeBatchId = Convert.ToInt32(lastBatchIdObj);
                    }

                    // Insert corresponding Run and mark as Active immediately (execution_order = 0), then get its ID in the same query
                    string fallbackRunName = $"{fallbackBatchName}-Me01";
                    string insertRunQuery = $"INSERT INTO `runs` (`batch_id`, `run_number`, `name`, `status`, `execution_order`, `start_time`, `created_at`) " +
                                            $"VALUES ({activeBatchId.Value}, 1, '{fallbackRunName}', 'Active', 0, '{DateTime.Now:yyyy-MM-dd HH:mm:ss}', NOW()); " +
                                            $"SELECT LAST_INSERT_ID();";
                    var lastRunIdObj = dataAccess.ExecuteScalarQuery(insertRunQuery);
                    if (lastRunIdObj != null && lastRunIdObj != DBNull.Value)
                    {
                        activeRunId = Convert.ToInt32(lastRunIdObj);
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"[AlarmReportLogger] No Pending run found. Created fallback Active Batch '{fallbackBatchName}' (ID: {activeBatchId}) and Run (ID: {activeRunId}).");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AlarmReportLogger] ERROR linking/creating batch: {ex.Message}");
                activeBatchId = null;
                activeRunId = null;
            }
        }

        private void CompleteActiveBatch()
        {
            if (activeRunId == null) return;
            
            try
            {
                dataAccess.ConnectionString = GetConnectionStringWithDb();
                string nowStr = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                // 1. Complete the active Run
                string completeRunQuery = $"UPDATE `runs` " +
                                           $"SET `status` = 'Completed', `end_time` = '{nowStr}' " +
                                           $"WHERE `id` = {activeRunId.Value}";
                dataAccess.ExecuteNonQuery(completeRunQuery);
                System.Diagnostics.Debug.WriteLine($"[AlarmReportLogger] Completed Run ID {activeRunId.Value} successfully.");

                // 2. Check if all runs in the parent Batch are finished (no Pending or Active runs left)
                string checkRemainingQuery = $"SELECT COUNT(*) FROM `runs` WHERE `batch_id` = {activeBatchId.Value} AND `status` IN ('Pending', 'Active')";
                var remainingObj = dataAccess.ExecuteScalarQuery(checkRemainingQuery);
                int remainingCount = remainingObj != null ? Convert.ToInt32(remainingObj) : 0;

                if (remainingCount == 0)
                {
                    // If no remaining runs, complete the parent Batch
                    string completeBatchQuery = $"UPDATE `batches` " +
                                                 $"SET `status` = 'Completed', `end_time` = '{nowStr}' " +
                                                 $"WHERE `id` = {activeBatchId.Value}";
                    dataAccess.ExecuteNonQuery(completeBatchQuery);
                    System.Diagnostics.Debug.WriteLine($"[AlarmReportLogger] Completed Batch ID {activeBatchId.Value} since all its runs are completed.");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[AlarmReportLogger] Batch ID {activeBatchId.Value} remains Active since {remainingCount} run(s) are still pending.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AlarmReportLogger] ERROR completing batch/run: {ex.Message}");
            }
            finally
            {
                activeRunId = null;
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
                sb.Append("`batchId` INT NULL DEFAULT NULL, ");
                sb.Append("`runId` INT NULL DEFAULT NULL");

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
                    value = GetScaledValueString(item.Alias, value);
                    fieldBuilder.Append($", `{item.Alias}`");
                    valueBuilder.Append($", '{value}'");
                }

                string batchIdValue = activeBatchId.HasValue ? activeBatchId.Value.ToString() : "NULL";
                string runIdValue = activeRunId.HasValue ? activeRunId.Value.ToString() : "NULL";

                var query = $"INSERT INTO `{TableName}` " +
                    $"(`DateTime`, `DeviceName`, `QuyTrinh`, `CongDoanMay`, `batchId`, `runId`{fieldBuilder}) " +
                    $"VALUES ('{DateTime.Now:yyyy-MM-dd HH:mm:ss}', '{deviceName}', {currentQuyTrinh}, {currentCongDoan}, {batchIdValue}, {runIdValue}{valueBuilder})";

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
                    "`runId` INT NULL DEFAULT NULL, " +
                    "`Severity` VARCHAR(50) NOT NULL DEFAULT 'ALARM', " +
                    "`restore_time` DATETIME NULL DEFAULT NULL" +
                    ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";
                dataAccess.ExecuteNonQuery(createTableSql);

                // Add columns just in case the table existed without them
                string[] checkCols = { "QuyTrinh", "CongDoan", "batchId", "runId", "Severity", "restore_time" };
                string[] alterSqls = {
                    $"ALTER TABLE `{tblName}` ADD COLUMN `QuyTrinh` INT NOT NULL DEFAULT 0",
                    $"ALTER TABLE `{tblName}` ADD COLUMN `CongDoan` VARCHAR(100) NOT NULL DEFAULT ''",
                    $"ALTER TABLE `{tblName}` ADD COLUMN `batchId` INT NULL DEFAULT NULL",
                    $"ALTER TABLE `{tblName}` ADD COLUMN `runId` INT NULL DEFAULT NULL AFTER `batchId`",
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
                            if (checkCols[i] == "runId")
                            {
                                // Migrate existing realtime alarm records based on batchId
                                string migrateLogsSql = $"UPDATE `{tblName}` t " +
                                                        "JOIN `runs` r ON t.batchId = r.batch_id " +
                                                        "SET t.runId = r.id " +
                                                        "WHERE t.runId IS NULL AND t.batchId IS NOT NULL";
                                dataAccess.ExecuteNonQuery(migrateLogsSql);
                            }
                        }
                    }
                    catch { }
                }

                // Check for duplicate INFO log within the same stage (CongDoan) of the same batch/process
                string checkDuplicateQuery;
                if (activeRunId.HasValue)
                {
                    checkDuplicateQuery = $"SELECT COUNT(*) FROM `{tblName}` " +
                                          $"WHERE `runId` = {activeRunId.Value} AND `CongDoan` = '{stageName}' AND `Severity` = 'INFO'";
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
                        System.Diagnostics.Debug.WriteLine($"[AlarmReportLogger] INFO event for stage '{stageName}' already logged in this batch/run. Skipping duplicate.");
                        return true;
                    }
                }

                string batchIdValue = activeBatchId.HasValue ? activeBatchId.Value.ToString() : "NULL";
                string runIdValue = activeRunId.HasValue ? activeRunId.Value.ToString() : "NULL";

                var query = $"INSERT INTO `{tblName}` " +
                    $"(`DateTime`, `DeviceName`, `TagName`, `Value`, `Threshold`, `Operator`, `Message`, `QuyTrinh`, `CongDoan`, `batchId`, `runId`, `Severity`) " +
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
                    $"{runIdValue}, " +
                    $"'INFO')";

                return dataAccess.ExecuteNonQuery(query) >= 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AlarmReportLogger] InsertRealtimeInfoEvent ERROR: {ex.Message}");
                return false;
            }
        }

        private void ResolveOrphanPauseRecords()
        {
            try
            {
                dataAccess.ConnectionString = GetConnectionStringWithDb();
                string tblName = "realtime_alarms";
                string formattedTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                string query = $"UPDATE `{tblName}` " +
                               $"SET `restore_time` = '{formattedTime}' " +
                               $"WHERE `DeviceName` = '{deviceName}' AND `TagName` = 'System' AND `Severity` = 'INFO' AND `Message` = 'Tạm dừng máy' AND `restore_time` IS NULL";

                int rows = dataAccess.ExecuteNonQuery(query);
                if (rows > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[AlarmReportLogger] Resolved {rows} orphan pause records for device {deviceName} at startup.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AlarmReportLogger] ResolveOrphanPauseRecords ERROR: {ex.Message}");
            }
        }

        private bool InsertPauseRecord()
        {
            try
            {
                if (!CreateDatabaseIfNotExists()) return false;

                dataAccess.ConnectionString = GetConnectionStringWithDb();
                string tblName = "realtime_alarms";
                
                string checkQuery = $"SELECT COUNT(*) FROM `{tblName}` WHERE `DeviceName` = '{deviceName}' AND `TagName` = 'System' AND `Severity` = 'INFO' AND `Message` = 'Tạm dừng máy' AND `restore_time` IS NULL";
                var countObj = dataAccess.ExecuteScalarQuery(checkQuery);
                if (countObj != null && countObj != DBNull.Value && Convert.ToInt32(countObj) > 0)
                {
                    return true; // Already logged, skip duplicate
                }

                string batchIdValue = activeBatchId.HasValue ? activeBatchId.Value.ToString() : "NULL";
                string runIdValue = activeRunId.HasValue ? activeRunId.Value.ToString() : "NULL";

                string query = $"INSERT INTO `{tblName}` " +
                    $"(`DateTime`, `DeviceName`, `TagName`, `Value`, `Threshold`, `Operator`, `Message`, `QuyTrinh`, `CongDoan`, `batchId`, `runId`, `Severity`, `restore_time`) " +
                    $"VALUES (" +
                    $"'{DateTime.Now:yyyy-MM-dd HH:mm:ss}', " +
                    $"'{deviceName}', " +
                    $"'System', " +
                    $"0, " +
                    $"0, " +
                    $"'=', " +
                    $"'Tạm dừng máy', " +
                    $"{currentQuyTrinh}, " +
                    $"'Tạm dừng', " +
                    $"{batchIdValue}, " +
                    $"{runIdValue}, " +
                    $"'INFO', " +
                    $"NULL)";

                return dataAccess.ExecuteNonQuery(query) >= 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AlarmReportLogger] InsertPauseRecord ERROR: {ex.Message}");
                return false;
            }
        }

        private bool UpdatePauseRecord(DateTime restoreTime)
        {
            try
            {
                dataAccess.ConnectionString = GetConnectionStringWithDb();
                string tblName = "realtime_alarms";
                string formattedTime = restoreTime.ToString("yyyy-MM-dd HH:mm:ss");

                string query = $"UPDATE `{tblName}` " +
                               $"SET `restore_time` = '{formattedTime}' " +
                               $"WHERE `DeviceName` = '{deviceName}' AND `TagName` = 'System' AND `Severity` = 'INFO' AND `Message` = 'Tạm dừng máy' AND `restore_time` IS NULL";

                int rows = dataAccess.ExecuteNonQuery(query);
                if (rows > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[AlarmReportLogger] Resolved pause record for device {deviceName} at {formattedTime}.");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AlarmReportLogger] UpdatePauseRecord ERROR: {ex.Message}");
                return false;
            }
        }

        private void CheckAndLogStageDurationAlarm(string tagNo, double actualDuration)
        {
            if (activeRunId == null) return;

            string spColumn = GetSetpointColumnName(tagNo);
            if (string.IsNullOrEmpty(spColumn)) return;

            try
            {
                dataAccess.ConnectionString = GetConnectionStringWithDb();

                // 1. Get Setpoint from runs table
                string getSetpointQuery = string.Format("SELECT `{0}` FROM `runs` WHERE `id` = {1}", spColumn, activeRunId.Value);
                var spObj = dataAccess.ExecuteScalarQuery(getSetpointQuery);
                if (spObj == null || spObj == DBNull.Value) return;
                double setpoint = Convert.ToDouble(spObj);

                // 2. Get Threshold and full TagName from alarmsettings table
                string getSettingQuery = string.Format("SELECT `TagName`, `Value` FROM `alarmsettings` WHERE `TagNo` = '{0}' AND `TagName` LIKE '%{1}%' LIMIT 1", tagNo, deviceName);
                var dtSetting = dataAccess.ExecuteQuery(getSettingQuery);
                if (dtSetting == null || dtSetting.Rows.Count == 0) return;

                string alarmTagName = dtSetting.Rows[0]["TagName"].ToString();
                string thresholdStr = dtSetting.Rows[0]["Value"].ToString();
                double threshold = 0;
                double.TryParse(thresholdStr, out threshold);

                // 3. Compare: |Actual - Setpoint| > Threshold
                double diff = Math.Abs(actualDuration - setpoint);
                if (diff > threshold)
                {
                    string stageDisplayName = GetStageDisplayName(tagNo);
                    string message = string.Format("[Cảnh báo] Giai đoạn {0} có thời gian thực tế ({1}s) chênh lệch vượt ngưỡng cho phép ({2}s) so với cài đặt ({3}s).",
                        stageDisplayName, actualDuration, threshold, setpoint);

                    InsertRealtimeStageAlarm(alarmTagName, actualDuration, threshold, message, tagNo);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("[AlarmReportLogger] CheckAndLogStageDurationAlarm ERROR: {0}", ex.Message));
            }
        }

        private string GetSetpointColumnName(string tagNo)
        {
            switch (tagNo)
            {
                case "T001": return "sp_thoi_gian_cap_lieu";
                case "T002": return "sp_thoi_gian_tron1";
                case "T003": return "sp_thoi_gian_xa_day";
                case "T004": return "sp_thoi_gian_rung_xa_day";
                case "T005": return "sp_thoi_gian_hut_xa_day_them";
                case "T006": return "sp_thoi_gian_tron2";
                case "T007": return "sp_thoi_gian_xa_hang";
                case "T008": return "sp_thoi_gian_rung_xa_hang";
                default: return null;
            }
        }

        private string GetStageDisplayName(string tagNo)
        {
            switch (tagNo)
            {
                case "T001": return "Cấp liệu";
                case "T002": return "Trộn 1";
                case "T003": return "Xả đáy";
                case "T004": return "Rung xả đáy";
                case "T005": return "Hút xả đáy";
                case "T006": return "Trộn 2";
                case "T007": return "Xả hàng";
                case "T008": return "Rung xả hàng";
                default: return tagNo;
            }
        }

        private bool InsertRealtimeStageAlarm(string tagName, double value, double threshold, string message, string tagNo)
        {
            try
            {
                if (!CreateDatabaseIfNotExists()) return false;

                dataAccess.ConnectionString = GetConnectionStringWithDb();

                string tblName = "realtime_alarms";
                string batchIdValue = activeBatchId.HasValue ? activeBatchId.Value.ToString() : "NULL";
                string runIdValue = activeRunId.HasValue ? activeRunId.Value.ToString() : "NULL";

                // Check for duplicate active stage alarm (once per run)
                if (activeRunId.HasValue)
                {
                    string checkDupQuery = string.Format(
                        "SELECT COUNT(*) FROM `{0}` WHERE `runId` = {1} AND `TagName` = '{2}' AND `Severity` = 'INFO'",
                        tblName, activeRunId.Value, tagName);
                    var dupObj = dataAccess.ExecuteScalarQuery(checkDupQuery);
                    if (dupObj != null && dupObj != DBNull.Value && Convert.ToInt32(dupObj) > 0)
                    {
                        System.Diagnostics.Debug.WriteLine(string.Format("[AlarmReportLogger] Stage duration alarm for '{0}' already logged in this run. Skipping duplicate.", tagName));
                        return true;
                    }
                }

                var query = string.Format(
                    "INSERT INTO `{0}` (`DateTime`, `DeviceName`, `TagName`, `Value`, `Threshold`, `Operator`, `Message`, `QuyTrinh`, `CongDoan`, `batchId`, `runId`, `Severity`, `restore_time`) " +
                    "VALUES ('{1:yyyy-MM-dd HH:mm:ss}', '{2}', '{3}', {4}, {5}, '>', '{6}', {7}, '{8}', {9}, {10}, 'INFO', '{1:yyyy-MM-dd HH:mm:ss}')",
                    tblName, DateTime.Now, deviceName, tagName, value, threshold, message, currentQuyTrinh, tagNo, batchIdValue, runIdValue);

                return dataAccess.ExecuteNonQuery(query) >= 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("[AlarmReportLogger] InsertRealtimeStageAlarm ERROR: {0}", ex.Message));
                return false;
            }
        }

        #endregion
    }
}
