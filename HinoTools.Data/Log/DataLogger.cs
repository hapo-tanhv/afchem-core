using ATSCADA;
using ATSCADA.ToolExtensions.ExtensionMethods;
using HinoTools.Data.Database;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HinoTools.Data.Log
{
    public partial class DataLogger : Component
    {
        private DataAccess dataAccess;

        private System.Timers.Timer tmrLog;
        
        private DateTime endTime;

        private iDriver driver;

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
        public string TableName { get; set; } = "datalog";

        [Category("Hino Settings")]    
        [Description("Format: TagName;Alias")]
        public string[] Collection { get; set; }

        [Category("Hino Settings")]
        public TimeUnit TimeUnit { get; set; }

        public DataLogger()
        {
            InitializeComponent();
        }

        public DataLogger(IContainer container)
        {
            container.Add(this);
            InitializeComponent();
        }

        private void Driver_ConstructionCompleted()
        {
            var logItems = GetLogItems().ToList();
            if (logItems.Count == 0) return;
            
            this.dataAccess = new DataAccess();
            this.tmrLog = new System.Timers.Timer();
            this.tmrLog.Interval = 5000;
            this.tmrLog.AutoReset = false;
            this.tmrLog.Elapsed += (sender, e) => LogData(logItems);

            var dateTime = DateTime.Now;           
            this.endTime = dateTime.GetEndTimeWithUnit(TimeUnit);
            this.tmrLog.Start();
        }
        
        private IEnumerable<LogItem> GetLogItems()
        {
            foreach(var item in Collection)
            {
                var itemSplit = item.Split(';');
                if (itemSplit.Length != 2) continue;

                yield return new LogItem()
                {
                    Tag = this.driver.GetTagByName(itemSplit[0]),
                    Alias = itemSplit[1]
                };
            }
        }

        private void LogData(List<LogItem> items)
        {
            try
            {
                this.tmrLog.Stop();

                var dateTimeNow = DateTime.Now;
                if(dateTimeNow > this.endTime)
                {
                    var timeLog = dateTimeNow.GetStartTimeWithUnit(TimeUnit);
                    if (CreateDatabaseIfNotExists())
                        if (CreateTableIfNotExists(items))
                            InsertData(timeLog, items);

                    this.endTime = dateTimeNow.GetEndTimeWithUnit(TimeUnit);
                }

                this.tmrLog.Start();
            }
            catch
            {
                this.tmrLog.Start();
            }
        }

        public bool CreateDatabaseIfNotExists()
        {
            try
            {
                dataAccess.ConnectionString =
                    $"Server={ServerName};" +
                    $"Uid={UserID};" +
                    $"Pwd={Password};";

                var query = $"create database if not exists {DatabaseName}";
                return dataAccess.ExecuteNonQuery(query) < 0 ? false : true;
            }
            catch { return false; }
        }

        public bool CreateTableIfNotExists(List<LogItem> items)
        {
            try
            {
                dataAccess.ConnectionString =
                    $"Server={ServerName};" +
                    $"Uid={UserID};" +
                    $"Pwd={Password};" +
                    $"Database={DatabaseName}";

                var fieldBuilder = new StringBuilder();
                foreach (var item in items)
                    fieldBuilder.Append($", `{item.Alias}` varchar(200) not null");

                var query = $"create table if not exists {TableName} (`DateTime` datetime not null {fieldBuilder})";
                return this.dataAccess.ExecuteNonQuery(query) < 0 ? false : true;
            }
            catch { return false; }
        }

        public bool InsertData(DateTime dateTime, List<LogItem> items)
        {
            try
            {
                dataAccess.ConnectionString =
                    $"Server={ServerName};" +
                    $"Uid={UserID};" +
                    $"Pwd={Password};" +
                    $"Database={DatabaseName}";

                var fieldBuilder = new StringBuilder();
                var valueBuilder = new StringBuilder();
                foreach (var item in items)
                {
                    var value = item.Tag.Value ?? "";

                    fieldBuilder.Append($", `{item.Alias}`");
                    valueBuilder.Append($", '{value}'");
                }

                var query = $"insert into {TableName} (`DateTime` {fieldBuilder}) values ('{dateTime:yyyy-MM-dd HH:mm:ss}' {valueBuilder})";
                return dataAccess.ExecuteNonQuery(query) < 0 ? false : true;
            }
            catch { return false; }
        }

    }
}
