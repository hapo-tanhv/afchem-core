using HinoTools.Alarm.Database;
using HinoTools.Alarm.Model.Event;
using System.ComponentModel;

namespace HinoTools.Alarm.Control
{
    public partial class EventLogger : Component
    {
        #region FIELDS 

        private DataAccess dataAccess;


        private IEventHub eventHub;

        #endregion

        #region PROPERTIES

        [Category("Hino Settings")]
        public IEventHub AlarmHub
        {
            get => this.eventHub;
            set
            {
                if (this.eventHub != null) this.eventHub.ConstructionCompleted -= ActionConstructionCompleted;
                this.eventHub = value;
                if (this.eventHub != null) this.eventHub.ConstructionCompleted += ActionConstructionCompleted;
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
        public string TableName { get; set; } = "eventlog";

        #endregion
        public EventLogger()
        {
            InitializeComponent();
        }

        public EventLogger(IContainer container)
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
                    this.eventHub.Pushed += InsertEvent;
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
                    $"`ID` int not null auto_increment, " +
                    $"`OccurrenceTime` DateTime not null, " +                   
                    $"`TagName` varchar(100) not null, " +
                    $"`TagNo` varchar(100) not null, " +
                    $"`Location` varchar(100) not null, " +
                    $"`Description` varchar(100) not null, " +
                    $"`Status` varchar(100) not null, " +
                    $"primary key (`ID`))";

                return this.dataAccess.ExecuteNonQuery(query) >= 0;
            }
            catch { return false; }
        }

        private void InsertEvent(EventItem eventItem)
        {
            try
            {
                this.dataAccess.ConnectionString = $"Server={ServerName};Uid={UserID};Pwd={Password}; Database={DatabaseName}";
                var status = eventItem.Status == EventStatus.ACTIVE ? eventItem.Param.ValueActive : eventItem.Param.ValueInactive;
                var query = $"insert into {TableName} (`OccurrenceTime`, `TagName`, `TagNo`, `Location`, `Description`, `Status`) values (" +
                    $"'{eventItem.OccurrenceTime:yyyy-MM-dd HH:mm:ss}', " +
                    $"'{eventItem.Param.TagName}', " +
                    $"'{eventItem.Param.TagNo}', " +
                    $"'{eventItem.Param.Location}', " +
                    $"'{eventItem.Param.Description}', " +
                    $"'{status}')";
                this.dataAccess.ExecuteNonQuery(query);
            }
            catch { }
        }

        #endregion
    }
}
