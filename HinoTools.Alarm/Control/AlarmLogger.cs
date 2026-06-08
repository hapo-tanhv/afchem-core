using HinoTools.Alarm.Database;
using HinoTools.Alarm.Model;
using System;
using System.ComponentModel;
using System.IO;

namespace HinoTools.Alarm.Control
{
    public partial class AlarmLogger : Component
    {

        #region FIELDS 

        private DataAccess dataAccess;


        private IAlarmHub alarmHub;

        #endregion

        #region PROPERTIES

        [Category("Hino Settings")]
        public IAlarmHub AlarmHub
        {
            get => this.alarmHub;
            set
            {
                if (this.alarmHub != null) this.alarmHub.ConstructionCompleted -= ActionConstructionCompleted;
                this.alarmHub = value;
                if (this.alarmHub != null) this.alarmHub.ConstructionCompleted += ActionConstructionCompleted;
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
        public string TableName { get; set; } = "alarmlog";

        #endregion

        public AlarmLogger()
        {
            InitializeComponent();
        }

        public AlarmLogger(IContainer container)
        {
            container.Add(this);
            InitializeComponent();
        }

        #region METHODS

        private void WriteDebugLog(string message)
        {
            try
            {
                string logPath = @"c:\Users\tanhv\Project\HinoTools.Alarm_27092023_Test\HinoTools.Alarm_27092023_Test\alarm_debug.log";
                File.AppendAllText(logPath, string.Format("{0:yyyy-MM-dd HH:mm:ss.fff} {1}{2}", DateTime.Now, message, Environment.NewLine));
            }
            catch { }
        }

        private void ActionConstructionCompleted()
        {
            this.dataAccess = new DataAccess();
            if (CreateDatabaseIfNotExists())
                if (CreateTableIfNotExists())
                {
                    AddBatchIdColumnIfNeeded(TableName);
                    AddRunIdColumnIfNeeded(TableName);
                    this.alarmHub.Pushed += InsertAlarm;
                }
        }

        private bool CreateDatabaseIfNotExists()
        {
            try
            {
                this.dataAccess.ConnectionString = $"Server={ServerName};Uid={UserID};Pwd={Password};";
                var query = $"create database if not exists {DatabaseName}";
                return this.dataAccess.ExecuteNonQuery(query) >= 0;
            }
            catch { return false; }
        }

        private bool CreateTableIfNotExists()
        {
            try
            {
                this.dataAccess.ConnectionString = $"Server={ServerName};Uid={UserID};Pwd={Password}; Database={DatabaseName}";
                var query = $"create table if not exists {TableName} (" +
                    $"`ID` varchar(100) not null, " +
                    $"`OccurrenceTime` DateTime null, " +
                    $"`RestoreTime` DateTime null, " +
                    $"`TagName` varchar(100) not null, " +
                    $"`TagNo` varchar(100) not null, " +
                    $"`Location` varchar(100) not null, " +
                    $"`Description` varchar(100) not null, " +
                    $"`Status` varchar(10) not null, " +
                    $"`FaultCode` int not null, " +
                    $"`batchId` INT NULL DEFAULT NULL, " +
                    $"`runId` INT NULL DEFAULT NULL, " +
                    $"primary key (`ID`))";

                return this.dataAccess.ExecuteNonQuery(query) >= 0;
            }
            catch { return false; }
        }

        private void AddBatchIdColumnIfNeeded(string tableName)
        {
            try
            {
                this.dataAccess.ConnectionString = $"Server={ServerName};Uid={UserID};Pwd={Password}; Database={DatabaseName}";
                string checkQuery = $"SHOW COLUMNS FROM `{tableName}` LIKE 'batchId'";
                var result = this.dataAccess.ExecuteScalarQuery(checkQuery);
                if (result == null || result == DBNull.Value)
                {
                    string alterQuery = $"ALTER TABLE `{tableName}` ADD COLUMN `batchId` INT NULL DEFAULT NULL";
                    this.dataAccess.ExecuteNonQuery(alterQuery);
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
                this.dataAccess.ConnectionString = $"Server={ServerName};Uid={UserID};Pwd={Password}; Database={DatabaseName}";
                string checkQuery = $"SHOW COLUMNS FROM `{tableName}` LIKE 'runId'";
                var result = this.dataAccess.ExecuteScalarQuery(checkQuery);
                if (result == null || result == DBNull.Value)
                {
                    string alterQuery = $"ALTER TABLE `{tableName}` ADD COLUMN `runId` INT NULL DEFAULT NULL AFTER `batchId`";
                    this.dataAccess.ExecuteNonQuery(alterQuery);
                    System.Diagnostics.Debug.WriteLine($"[Migration] Added runId column to {tableName} successfully.");

                    // Migrate existing historical log records based on batchId
                    string migrateLogsSql = $"UPDATE `{tableName}` t " +
                                            "JOIN `runs` r ON t.batchId = r.batch_id " +
                                            "SET t.runId = r.id " +
                                            "WHERE t.runId IS NULL AND t.batchId IS NOT NULL";
                    this.dataAccess.ExecuteNonQuery(migrateLogsSql);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Migration] ERROR adding runId column to {tableName}: {ex.Message}");
            }
        }

        private string ExtractDeviceName(string tagName)
        {
            WriteDebugLog(string.Format("[ExtractDeviceName] Input TagName: '{0}'", tagName));
            if (string.IsNullOrEmpty(tagName))
            {
                WriteDebugLog("[ExtractDeviceName] TagName is empty, fallback to TX01");
                return "TX01";
            }

            var dotIndex = tagName.IndexOf('.');
            string prefix = dotIndex > 0 ? tagName.Substring(0, dotIndex) : tagName;

            if (prefix.StartsWith("AFChem", StringComparison.OrdinalIgnoreCase) && prefix.Length > 6)
            {
                string dev = prefix.Substring(6);
                WriteDebugLog(string.Format("[ExtractDeviceName] Extracted: '{0}' from prefix '{1}'", dev, prefix));
                return dev;
            }

            WriteDebugLog(string.Format("[ExtractDeviceName] Prefix '{0}' does not match AFChem pattern. Fallback to TX01.", prefix));
            return "TX01";
        }

        private void GetActiveBatchAndRunId(string deviceName, bool isNewRunStart, out int? batchId, out int? runId)
        {
            batchId = null;
            runId = null;
            WriteDebugLog(string.Format("[GetActiveBatchAndRunId] Start query for device: '{0}', isNewRunStart: {1}", deviceName, isNewRunStart));
            try
            {
                this.dataAccess.ConnectionString = $"Server={ServerName};Uid={UserID};Pwd={Password}; Database={DatabaseName}";
                string nowStr = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                if (isNewRunStart)
                {
                    // 1. Find the current Active run
                    string activeRunQuery = "SELECT r.id, r.batch_id FROM `runs` r " +
                                            "JOIN `batches` b ON r.batch_id = b.id " +
                                            $"WHERE b.device_name = '{deviceName}' AND r.status = 'Active' " +
                                            "ORDER BY r.id DESC LIMIT 1";
                    var activeRes = this.dataAccess.ExecuteQuery(activeRunQuery);
                    if (activeRes != null && activeRes.Rows.Count > 0)
                    {
                        int activeRId = Convert.ToInt32(activeRes.Rows[0]["id"]);
                        int activeBId = Convert.ToInt32(activeRes.Rows[0]["batch_id"]);

                        // Mark this run as Error because it was aborted by a new run start
                        string completeRunQuery = string.Format("UPDATE `runs` SET `status` = 'Error', `end_time` = '{0}' WHERE `id` = {1}", nowStr, activeRId);
                        this.dataAccess.ExecuteNonQuery(completeRunQuery);

                        // Create a compensating run for the parent batch
                        try
                        {
                            string infoQuery = string.Format("SELECT name, total_runs FROM `batches` WHERE `id` = {0}", activeBId);
                            var infoDt = this.dataAccess.ExecuteQuery(infoQuery);
                            if (infoDt != null && infoDt.Rows.Count > 0)
                            {
                                string batchName = infoDt.Rows[0]["name"].ToString();
                                int currentTotalRuns = Convert.ToInt32(infoDt.Rows[0]["total_runs"]);

                                int newRunNumber = currentTotalRuns + 1;
                                string newRunName = string.Format("{0}-Me{1:D2}", batchName, newRunNumber);

                                // Update total_runs in batches
                                string updateBatchRunsQuery = string.Format("UPDATE `batches` SET `total_runs` = {0} WHERE `id` = {1}", newRunNumber, activeBId);
                                this.dataAccess.ExecuteNonQuery(updateBatchRunsQuery);

                                // Insert new compensating run as Pending
                                string insertCompensatingRunQuery = string.Format(
                                    "INSERT INTO `runs` (`batch_id`, `run_number`, `name`, `status`, `created_at`) VALUES ({0}, {1}, '{2}', 'Pending', NOW())",
                                    activeBId, newRunNumber, newRunName);
                                this.dataAccess.ExecuteNonQuery(insertCompensatingRunQuery);
                            }
                        }
                        catch (Exception ex)
                        {
                            WriteDebugLog(string.Format("[GetActiveBatchAndRunId] ERROR creating compensating run: {0}", ex.Message));
                        }

                        // Check if all runs in this batch are finished (no Pending or Active runs left)
                        string checkRemQuery = string.Format("SELECT COUNT(*) FROM `runs` WHERE `batch_id` = {0} AND `status` IN ('Pending', 'Active')", activeBId);
                        var remObj = this.dataAccess.ExecuteScalarQuery(checkRemQuery);
                        int remCount = remObj != null ? Convert.ToInt32(remObj) : 0;
                        if (remCount == 0)
                        {
                            string completeBatchQuery = string.Format("UPDATE `batches` SET `status` = 'Completed', `end_time` = '{0}' WHERE `id` = {1}", nowStr, activeBId);
                            this.dataAccess.ExecuteNonQuery(completeBatchQuery);
                        }
                    }
                }

                // 2. Try to find the Active run first
                string query = "SELECT r.id, r.batch_id FROM `runs` r " +
                               "JOIN `batches` b ON r.batch_id = b.id " +
                               $"WHERE b.device_name = '{deviceName}' AND r.status = 'Active' " +
                               "ORDER BY r.id DESC LIMIT 1";
                WriteDebugLog(string.Format("[GetActiveBatchAndRunId] Query 1 (Active): {0}", query));
                var dt = this.dataAccess.ExecuteQuery(query);
                if (dt != null && dt.Rows.Count > 0)
                {
                    runId = Convert.ToInt32(dt.Rows[0]["id"]);
                    batchId = Convert.ToInt32(dt.Rows[0]["batch_id"]);
                    WriteDebugLog(string.Format("[GetActiveBatchAndRunId] Found Active Run ID: {0}, Batch ID: {1}", runId, batchId));
                    return;
                }

                // 3. Fallback: Find the oldest 'Pending' run (FIFO)
                string fallbackQuery = "SELECT r.id, r.batch_id, b.status as batch_status FROM `runs` r " +
                                       "JOIN `batches` b ON r.batch_id = b.id " +
                                       $"WHERE b.device_name = '{deviceName}' AND r.status = 'Pending' " +
                                       "ORDER BY b.id ASC, r.run_number ASC LIMIT 1";
                WriteDebugLog(string.Format("[GetActiveBatchAndRunId] Query 2 (Pending FIFO): {0}", fallbackQuery));
                dt = this.dataAccess.ExecuteQuery(fallbackQuery);
                if (dt != null && dt.Rows.Count > 0)
                {
                    runId = Convert.ToInt32(dt.Rows[0]["id"]);
                    batchId = Convert.ToInt32(dt.Rows[0]["batch_id"]);
                    string batchStatus = dt.Rows[0]["batch_status"].ToString();

                    // Update run to Active
                    string updateRun = $"UPDATE `runs` SET `status` = 'Active', `start_time` = '{nowStr}' WHERE `id` = {runId}";
                    this.dataAccess.ExecuteNonQuery(updateRun);

                    // Update batch to Active if Pending
                    if (batchStatus == "Pending")
                    {
                        string updateBatch = $"UPDATE `batches` SET `status` = 'Active', `start_time` = '{nowStr}' WHERE `id` = {batchId}";
                        this.dataAccess.ExecuteNonQuery(updateBatch);
                    }

                    WriteDebugLog(string.Format("[GetActiveBatchAndRunId] Activated FIFO pending Run ID: {0}, Batch ID: {1}", runId, batchId));
                    return;
                }

                // 4. Fallback: Auto-generate a fallback/emergency batch and run
                string todayStr = DateTime.Now.ToString("yyyyMMdd");
                int nextStt = 1;

                string selectLastQuery = $"SELECT `name` FROM `batches` " +
                                         $"WHERE `device_name` = '{deviceName}' AND DATE(`created_at`) = CURDATE() " +
                                         $"ORDER BY `id` DESC LIMIT 1";
                var lastObj = this.dataAccess.ExecuteScalarQuery(selectLastQuery);
                if (lastObj != null && lastObj != DBNull.Value)
                {
                    var parts = lastObj.ToString().Split('-');
                    if (parts.Length >= 3 && int.TryParse(parts[parts.Length - 1], out int lastSttVal))
                    {
                        nextStt = lastSttVal + 1;
                    }
                }

                string fallbackBatchName = $"{deviceName}-{todayStr}-{nextStt:D2}";
                string insertBatch = $"INSERT INTO `batches` (`name`, `device_name`, `status`, `total_runs`, `start_time`, `created_at`) " +
                                     $"VALUES ('{fallbackBatchName}', '{deviceName}', 'Active', 1, '{nowStr}', NOW())";
                this.dataAccess.ExecuteNonQuery(insertBatch);

                string getLastBatchId = "SELECT LAST_INSERT_ID()";
                var lastBatchIdObj = this.dataAccess.ExecuteScalarQuery(getLastBatchId);
                if (lastBatchIdObj != null && lastBatchIdObj != DBNull.Value)
                {
                    batchId = Convert.ToInt32(lastBatchIdObj);
                }

                string fallbackRunName = $"{fallbackBatchName}-Me01";
                string insertRun = $"INSERT INTO `runs` (`batch_id`, `run_number`, `name`, `status`, `start_time`, `created_at`) " +
                                   $"VALUES ({batchId.Value}, 1, '{fallbackRunName}', 'Active', '{nowStr}', NOW())";
                this.dataAccess.ExecuteNonQuery(insertRun);

                string getLastRunId = "SELECT LAST_INSERT_ID()";
                var lastRunIdObj = this.dataAccess.ExecuteScalarQuery(getLastRunId);
                if (lastRunIdObj != null && lastRunIdObj != DBNull.Value)
                {
                    runId = Convert.ToInt32(lastRunIdObj);
                }

                WriteDebugLog(string.Format("[GetActiveBatchAndRunId] Auto-created emergency Active Batch (ID: {0}) and Run (ID: {1})", batchId, runId));
            }
            catch (Exception ex)
            {
                WriteDebugLog(string.Format("[GetActiveBatchAndRunId] EXCEPTION: {0}{1}{2}", ex.Message, Environment.NewLine, ex.StackTrace));
            }
        }

        private void InsertAlarm(AlarmItem alarmItem)
        {
            WriteDebugLog(string.Format("[InsertAlarm] Start for ID: '{0}', TagName: '{1}', Status: '{2}'", 
                alarmItem.ID, 
                alarmItem.Param == null ? "null" : alarmItem.Param.TagName, 
                alarmItem.Status));
            try
            {
                this.dataAccess.ConnectionString = $"Server={ServerName};Uid={UserID};Pwd={Password}; Database={DatabaseName}";
                var occurrenceTime = alarmItem.OccurrenceTime == DateTime.MinValue ?
                    $"'{DateTime.Now:yyyy-MM-dd HH:mm:ss}'" :
                    $"'{alarmItem.OccurrenceTime:yyyy-MM-dd HH:mm:ss}'";
                var restoreTime = alarmItem.RestoreTime == DateTime.MinValue ? "null" : $"'{alarmItem.RestoreTime:yyyy-MM-dd HH:mm:ss}'";
                var status = alarmItem.Status == AlarmStatus.ALARM ? "Alarm" : "Resolved";

                if (alarmItem.Param == null)
                {
                    WriteDebugLog("[InsertAlarm] ERROR: alarmItem.Param is NULL!");
                    return;
                }

                string deviceName = ExtractDeviceName(alarmItem.Param.TagName);
                bool isNewRunStart = alarmItem.Param.TagName.EndsWith("ThoiGianCapLieu", StringComparison.OrdinalIgnoreCase) 
                                       && alarmItem.Status == AlarmStatus.ALARM;

                // Check if an active alarm for this tag already exists in database for the active run of this device
                string activeAlarmId = null;
                int? existingActiveRunId = null;
                try
                {
                    string activeRunQuery = string.Format(
                        "SELECT r.id FROM `runs` r JOIN `batches` b ON r.batch_id = b.id WHERE b.device_name = '{0}' AND r.status = 'Active' ORDER BY r.id DESC LIMIT 1",
                        deviceName);
                    var activeRes = this.dataAccess.ExecuteQuery(activeRunQuery);
                    if (activeRes != null && activeRes.Rows.Count > 0)
                    {
                        existingActiveRunId = Convert.ToInt32(activeRes.Rows[0]["id"]);

                        string checkActiveSql = string.Format(
                            "SELECT `ID` FROM `{0}` WHERE `TagName` = '{1}' AND `runId` = {2} AND (`Status` = 'Alarm' OR `RestoreTime` IS NULL) ORDER BY `OccurrenceTime` DESC LIMIT 1",
                            TableName, alarmItem.Param.TagName, existingActiveRunId.Value);
                        var activeAlarmRes = this.dataAccess.ExecuteQuery(checkActiveSql);
                        if (activeAlarmRes != null && activeAlarmRes.Rows.Count > 0)
                        {
                            activeAlarmId = activeAlarmRes.Rows[0]["ID"].ToString();
                        }
                    }
                }
                catch (Exception ex)
                {
                    WriteDebugLog(string.Format("[InsertAlarm] Error pre-checking active alarm: {0}", ex.Message));
                }

                // If an active alarm already exists for this tag in the active run, it is a restart, not a new run start
                if (activeAlarmId != null)
                {
                    isNewRunStart = false;
                }

                int? batchId;
                int? runId;
                GetActiveBatchAndRunId(deviceName, isNewRunStart, out batchId, out runId);
                string batchIdValue = batchId.HasValue ? batchId.Value.ToString() : "null";
                string runIdValue = runId.HasValue ? runId.Value.ToString() : "null";

                // Update activeAlarmId using the resolved runId if not already found via active run
                if (activeAlarmId == null && runId.HasValue)
                {
                    try
                    {
                        string checkActiveSql = string.Format(
                            "SELECT `ID` FROM `{0}` WHERE `TagName` = '{1}' AND `runId` = {2} AND (`Status` = 'Alarm' OR `RestoreTime` IS NULL) ORDER BY `OccurrenceTime` DESC LIMIT 1",
                            TableName, alarmItem.Param.TagName, runId.Value);
                        var activeAlarmRes = this.dataAccess.ExecuteQuery(checkActiveSql);
                        if (activeAlarmRes != null && activeAlarmRes.Rows.Count > 0)
                        {
                            activeAlarmId = activeAlarmRes.Rows[0]["ID"].ToString();
                        }
                    }
                    catch { }
                }

                if (alarmItem.Status == AlarmStatus.ALARM)
                {
                    if (activeAlarmId != null)
                    {
                        WriteDebugLog(string.Format("[InsertAlarm] Active alarm already exists in database with ID: '{0}'. Skipping duplicate alarm insert.", activeAlarmId));
                        return; // Skip duplicate active alarm insertion
                    }
                }
                else // Resolved
                {
                    if (activeAlarmId != null)
                    {
                        string updateSql = string.Format(
                            "UPDATE `{0}` SET `RestoreTime` = {1}, `Status` = '{2}' WHERE `ID` = '{3}'",
                            TableName, restoreTime, status, activeAlarmId);
                        WriteDebugLog(string.Format("[InsertAlarm] Updating existing active alarm ID: '{0}' to Resolved. Query: {1}", activeAlarmId, updateSql));
                        int rowsUpdated = this.dataAccess.ExecuteNonQuery(updateSql);
                        WriteDebugLog(string.Format("[InsertAlarm] Update success. Rows affected: {0}", rowsUpdated));
                        return; // Skip insert, we updated the existing one instead
                    }
                }

                var query = $"insert into {TableName} " +
                    $"(`ID`, `OccurrenceTime`, `RestoreTime`, `TagName`, `TagNo`, `Location`, `Description`, `Status`, `FaultCode`, `batchId`, `runId`) " +
                    $"values (" +
                    $"'{alarmItem.ID}', " +
                    $"{occurrenceTime}, " +
                    $"{restoreTime}, " +
                    $"'{alarmItem.Param.TagName}', " +
                    $"'{alarmItem.Param.TagNo}', " +
                    $"'{alarmItem.Param.Location}', " +
                    $"'{alarmItem.Param.Description}', " +
                    $"'{status}'," +
                    $"{alarmItem.Param.FaultCode}," +
                    $"{batchIdValue}," +
                    $"{runIdValue}) " +
                    $"on duplicate key update " +
                    $"`RestoreTime` = values(`RestoreTime`), " +
                    $"`Status` = values(`Status`) ";

                WriteDebugLog(string.Format("[InsertAlarm] Executing query: {0}", query));
                int rows = this.dataAccess.ExecuteNonQuery(query);
                WriteDebugLog(string.Format("[InsertAlarm] Execution success. Rows affected: {0}", rows));
            }
            catch(Exception e )
            {
                WriteDebugLog(string.Format("[InsertAlarm] EXCEPTION: {0}{1}{2}", e.Message, Environment.NewLine, e.StackTrace));
                return;
            }
        }

        #endregion
    }
}
