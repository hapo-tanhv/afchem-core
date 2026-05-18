using HinoTools.Alarm.Database;
using HinoTools.Alarm.Model;
using System;
using System.ComponentModel;

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
        public string Password { get; set; } = "100100";

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

        private void ActionConstructionCompleted()
        {
            this.dataAccess = new DataAccess();
            if (CreateDatabaseIfNotExists())
                if (CreateTableIfNotExists())
                    this.alarmHub.Pushed += InsertAlarm;
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
                    $"primary key (`ID`))";

                return this.dataAccess.ExecuteNonQuery(query) >= 0;
            }
            catch { return false; }
        }

        private void InsertAlarm(AlarmItem alarmItem)
        {
            try
            {
                this.dataAccess.ConnectionString = $"Server={ServerName};Uid={UserID};Pwd={Password}; Database={DatabaseName}";
                var occurrenceTime = alarmItem.OccurrenceTime == DateTime.MinValue ?
                    $"'{DateTime.Now:yyyy-MM-dd HH:mm:ss}'" :
                    $"'{alarmItem.OccurrenceTime:yyyy-MM-dd HH:mm:ss}'";
                var restoreTime = alarmItem.RestoreTime == DateTime.MinValue ? "null" : $"'{alarmItem.RestoreTime:yyyy-MM-dd HH:mm:ss}'";
                var status = alarmItem.Status == AlarmStatus.ALARM ? "Alarm" : "Resolved";
                var query = $"insert into {TableName} " +
                    $"(`ID`, `OccurrenceTime`, `RestoreTime`, `TagName`, `TagNo`, `Location`, `Description`, `Status`, `FaultCode`) " +
                    $"values (" +
                    $"'{alarmItem.ID}', " +
                    $"{occurrenceTime}, " +
                    $"{restoreTime}, " +
                    $"'{alarmItem.Param.TagName}', " +
                    $"'{alarmItem.Param.TagNo}', " +
                    $"'{alarmItem.Param.Location}', " +
                    $"'{alarmItem.Param.Description}', " +
                    $"'{status}'," +
                    $"{alarmItem.Param.FaultCode}) " +
                    $"on duplicate key update " +
                    $"`RestoreTime` = values(`RestoreTime`), " +
                    $"`Status` = values(`Status`) ";

                this.dataAccess.ExecuteNonQuery(query);
            }
            catch(Exception e )
            {
                return;
            }
        }

        #endregion
    }
}
