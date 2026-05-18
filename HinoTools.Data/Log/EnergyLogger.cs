using ATSCADA;
using ATSCADA.ToolExtensions.ExtensionMethods;
using ATSCADA.ToolExtensions.TagCollection;
using HinoTools.Data.Database;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Design;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HinoTools.Data.Log
{
    public partial class EnergyLogger : Component
    {
        #region FIELDS

        private DataAccess dataAccess;

        private bool isInitValueFirstTime;

        private bool isBusy;

        private ITag energyTag;

        private double startValue;

        private DateTime startTime;

        private DateTime endTime;

        private iDriver driver;

        #endregion

        #region DATA PROPERTIES

        [Category("Hino Settings - Data")]
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

        [Category("Hino Settings - Data")]
        [Description("Current power tag name.")]
        [Editor(typeof(SmartTagEditor), typeof(UITypeEditor))]
        public string EnergyTagName { get; set; }

        [Category("ATSCADA - Database Settings")]
        [Description("Name of device.")]
        public string DeviceName { get; set; } = "Device";

        [Category("ATSCADA - Database Settings")]
        [Description("Name of power group.")]
        public string GroupName { get; set; } = "Group";
        
        [Category("ATSCADA - Database Settings")]
        public TimeUnit TimeUnit { get; set; }

        #endregion

        #region DATABASE PROPERTIES

        [Category("Hino Settings")]
        public string ServerName { get; set; } = "localhost";

        [Category("Hino Settings")]
        public string UserID { get; set; } = "root";

        [Category("Hino Settings")]
        public string Password { get; set; } = "100100";

        [Category("Hino Settings")]
        public string DatabaseName { get; set; } = "scada";

        [Category("Hino Settings")]
        public string TableName { get; set; } = "energy_log";

        #endregion
        public EnergyLogger()
        {
            InitializeComponent();
        }

        public EnergyLogger(IContainer container)
        {
            container.Add(this);
            InitializeComponent();
        }

        #region METHODS

        private void Driver_ConstructionCompleted()
        {
            this.energyTag = this.driver.GetTagByName(EnergyTagName);
            if (this.energyTag is null) return;            

            this.dataAccess = new DataAccess();            
            this.energyTag.TagValueChanged += (sender, e) => InitValue();
            this.energyTag.TagStatusChanged += (sender, e) => InitValue();
            this.energyTag?.SetActionWithTaskActed(UpdateValue);

            InitValue();
        }

        private void InitValue()
        {
            if (this.isInitValueFirstTime) return;
            if (double.TryParse(this.energyTag.Value, out double startValue))
            {
                this.startValue = startValue;

                var dateTimeNow = DateTime.Now;
                this.startTime = dateTimeNow.GetStartTimeWithUnit(TimeUnit);
                this.endTime = dateTimeNow.GetEndTimeWithUnit(TimeUnit);

                this.isInitValueFirstTime = true;
            }
        }

        private void UpdateValue(DateTime timeStamp)
        {
            if (!this.isInitValueFirstTime) return;
            if (this.isBusy) return;
            this.isBusy = true;

            if (timeStamp > this.endTime)
            {
                var energyValue = 0d;
                if (double.TryParse(this.energyTag.Value, out double value))
                {
                    energyValue = value - this.startValue;
                    this.startValue = value;
                }

                if (energyValue >= 0 &&
                    CreateDatabaseIfNotExists() &&
                    CreateTableIfNotExists())
                    LogEnergy(energyValue);

                this.startTime = timeStamp.GetStartTimeWithUnit(TimeUnit);
                this.endTime = timeStamp.GetEndTimeWithUnit(TimeUnit);
            }

            this.isBusy = false;
        }

        #endregion

        #region DATABASE

        public bool CreateDatabaseIfNotExists()
        {
            try
            {
                this.dataAccess.ConnectionString =
                    $"Server={ServerName};" +
                    $"Uid={UserID};" +
                    $"Pwd={Password};";

                var query = $"create database if not exists {DatabaseName}";

                return this.dataAccess.ExecuteNonQuery(query) < 0 ? false : true;
            }
            catch { return false; }
        }

        public bool CreateTableIfNotExists()
        {
            try
            {
                this.dataAccess.ConnectionString =
                    $"Server={ServerName};" +
                    $"Uid={UserID};" +
                    $"Pwd={Password};" +
                    $"Database={DatabaseName}";

                var query = $"create table if not exists `{TableName}` (" +
                    $"`start_time` datetime not null, " +
                    $"`end_time` datetime not null, " +
                    $"`group_name` varchar(200) not null, " +
                    $"`device_name` varchar(200) not null, " +
                    $"`device_tag_name` varchar(200) not null, " +
                    $"`value` double not null default 0, " +
                    $"`direction` tinyint not null default 0, " +
                    $"`data_type` tinyint not null default 0, " +
                    $"`time_unit` tinyint not null default 0, " +
                    $"primary key (`start_time`, `end_time`, `group_name`, `device_name`, `device_tag_name`, `direction`, `data_type`, `time_unit`)" +
                    $")";

                return this.dataAccess.ExecuteNonQuery(query) < 0 ? false : true;
            }
            catch { return false; }
        }



        public bool LogEnergy(double value)
        {
            try
            {
                this.dataAccess.ConnectionString =
                    $"Server={ServerName};" +
                    $"Uid={UserID};" +
                    $"Pwd={Password};" +
                    $"Database={DatabaseName}";

                var command = $"insert into `{TableName}` " +
                    $"(`start_time`, `end_time`, `group_name`, `device_name`, `device_tag_name`, `value`, `direction`, `data_type`, `time_unit`) values (" +
                    $"'{this.startTime:yyyy-MM-dd HH:mm:ss}', " +
                    $"'{this.endTime:yyyy-MM-dd HH:mm:ss}', " +
                    $"'{GroupName}', " +
                    $"'{DeviceName}', " +
                    $"'{EnergyTagName}', " +
                    $"{value}, " +
                    $"{(int)1}, " +
                    $"{(int)0}, " +
                    $"{(int)TimeUnit}) " +
                    $"on duplicate key update `value` = {value};";

                return this.dataAccess.ExecuteNonQuery(command) < 0 ? false : true;
            }
            catch { return false; }
        }

        #endregion
    }
}
