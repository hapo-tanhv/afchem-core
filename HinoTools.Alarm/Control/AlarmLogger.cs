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

        private int? GetActiveBatchId(string deviceName)
        {
            WriteDebugLog(string.Format("[GetActiveBatchId] Start query for device: '{0}'", deviceName));
            try
            {
                this.dataAccess.ConnectionString = $"Server={ServerName};Uid={UserID};Pwd={Password}; Database={DatabaseName}";
                
                // 1. Try to find the Active batch first
                string query = $"SELECT `id` FROM `batches` WHERE `device_name` = '{deviceName}' AND `status` = 'Active' ORDER BY `id` DESC LIMIT 1";
                WriteDebugLog(string.Format("[GetActiveBatchId] Query 1 (Active): {0}", query));
                var result = this.dataAccess.ExecuteScalarQuery(query);
                WriteDebugLog(string.Format("[GetActiveBatchId] Result 1: {0}", result == null ? "null" : result.ToString()));
                if (result != null && result != DBNull.Value)
                {
                    int id = Convert.ToInt32(result);
                    WriteDebugLog(string.Format("[GetActiveBatchId] Found Active Batch ID: {0}", id));
                    return id;
                }

                // 2. Fallback: Find the oldest 'Pending' batch (FIFO)
                string fallbackQuery = $"SELECT `id` FROM `batches` WHERE `device_name` = '{deviceName}' AND `status` = 'Pending' ORDER BY `id` ASC LIMIT 1";
                WriteDebugLog(string.Format("[GetActiveBatchId] Query 2 (Pending FIFO): {0}", fallbackQuery));
                result = this.dataAccess.ExecuteScalarQuery(fallbackQuery);
                WriteDebugLog(string.Format("[GetActiveBatchId] Result 2: {0}", result == null ? "null" : result.ToString()));
                if (result != null && result != DBNull.Value)
                {
                    int id = Convert.ToInt32(result);
                    // Update this batch to Active and set start_time since we are starting it now!
                    string updateQuery = $"UPDATE `batches` " +
                                         $"SET `status` = 'Active', `start_time` = '{DateTime.Now:yyyy-MM-dd HH:mm:ss}' " +
                                         $"WHERE `id` = {id}";
                    WriteDebugLog(string.Format("[GetActiveBatchId] Activating FIFO pending batch ID {0}: {1}", id, updateQuery));
                    this.dataAccess.ExecuteNonQuery(updateQuery);
                    
                    WriteDebugLog(string.Format("[GetActiveBatchId] Found and activated Fallback Pending Batch ID: {0}", id));
                    return id;
                }

                // 3. Fallback: Auto-generate a fallback/emergency batch if none exists
                string todayStr = DateTime.Now.ToString("yyyyMMdd");
                int nextStt = 1;

                string selectLastQuery = $"SELECT `name` FROM `batches` " +
                                         $"WHERE `device_name` = '{deviceName}' AND DATE(`created_at`) = CURDATE() " +
                                         $"ORDER BY `id` DESC LIMIT 1";
                WriteDebugLog(string.Format("[GetActiveBatchId] Query 3 (Select last batch of today): {0}", selectLastQuery));
                var lastObj = this.dataAccess.ExecuteScalarQuery(selectLastQuery);
                WriteDebugLog(string.Format("[GetActiveBatchId] Result 3: {0}", lastObj == null ? "null" : lastObj.ToString()));
                if (lastObj != null && lastObj != DBNull.Value)
                {
                    var parts = lastObj.ToString().Split('-');
                    if (parts.Length >= 3 && int.TryParse(parts[parts.Length - 1], out int lastSttVal))
                    {
                        nextStt = lastSttVal + 1;
                    }
                }

                string fallbackBatchName = $"{deviceName}-{todayStr}-{nextStt:D2}";
                string insertQuery = $"INSERT INTO `batches` (`name`, `device_name`, `status`, `start_time`, `created_at`) " +
                                     $"VALUES ('{fallbackBatchName}', '{deviceName}', 'Active', '{DateTime.Now:yyyy-MM-dd HH:mm:ss}', NOW())";
                WriteDebugLog(string.Format("[GetActiveBatchId] Executing emergency insert: {0}", insertQuery));
                this.dataAccess.ExecuteNonQuery(insertQuery);

                string getLastIdQuery = "SELECT LAST_INSERT_ID()";
                var lastIdObj = this.dataAccess.ExecuteScalarQuery(getLastIdQuery);
                if (lastIdObj != null && lastIdObj != DBNull.Value)
                {
                    int lastId = Convert.ToInt32(lastIdObj);
                    WriteDebugLog(string.Format("[GetActiveBatchId] Auto-created emergency Active batch '{0}' with ID: {1}", fallbackBatchName, lastId));
                    return lastId;
                }
            }
            catch (Exception ex)
            {
                WriteDebugLog(string.Format("[GetActiveBatchId] EXCEPTION: {0}{1}{2}", ex.Message, Environment.NewLine, ex.StackTrace));
                System.Diagnostics.Debug.WriteLine($"[AlarmLogger] ERROR querying active batch: {ex.Message}");
            }
            WriteDebugLog("[GetActiveBatchId] No batch found and failed to create, returning null");
            return null;
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
                int? batchId = GetActiveBatchId(deviceName);
                string batchIdValue = batchId.HasValue ? batchId.Value.ToString() : "null";

                var query = $"insert into {TableName} " +
                    $"(`ID`, `OccurrenceTime`, `RestoreTime`, `TagName`, `TagNo`, `Location`, `Description`, `Status`, `FaultCode`, `batchId`) " +
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
                    $"{batchIdValue}) " +
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
