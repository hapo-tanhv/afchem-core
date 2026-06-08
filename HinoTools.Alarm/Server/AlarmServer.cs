using ATSCADA;
using HinoTools.Alarm.Model;
using HinoTools.Alarm.Database;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Threading;

namespace HinoTools.Alarm.Server
{
    public partial class AlarmServer : Component, IAlarmHub
    {
        #region FILEDS

        private DataAccess dataAccess;

        private readonly List<AlarmItem> alarmItems = new List<AlarmItem>();

        private readonly List<AlarmTagBase> alarmTags = new List<AlarmTagBase>();

        private iDriver driver;

        private readonly SemaphoreSlim mutex = new SemaphoreSlim(1, 1);

        #endregion

        #region PROPERTIES

        [Category("Hino Settings")]
        public iDriver Driver
        {
            get => this.driver;
            set
            {
                if (this.driver != null) this.driver.ConstructionCompleted -= ActionConstructionCompleted;
                this.driver = value;
                if (this.driver != null) this.driver.ConstructionCompleted += ActionConstructionCompleted;
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
        public string TableName { get; set; } = "alarmsettings";

        [Category("Hino Settings")]
        public string TableLog { get; set; } = "alarmlog";

        [Category("Hino Settings")]
        public int Limit { get; set; } = 20;

        [Browsable(false)]
        public int Count => this.alarmItems.Where(x => x.Status == AlarmStatus.ALARM).Count();

        [Browsable(false)]
        public bool IsActive { get; private set; }

        [Browsable(false)]
        public Quality ConnectionQuality => Quality.Good;

        public event Action ConstructionCompleted;

        public event Action<Quality> ConnectionQualityChanged;

        public event Action<AlarmItem> Pushed;

        public event Action<string[]> Acknowledged;

        public event Action Reseted;

        #endregion

        public AlarmServer()
        {
            InitializeComponent();
        }

        public AlarmServer(IContainer container)
        {
            container.Add(this);
            InitializeComponent();
        }

        #region LOAD

        private void ActionConstructionCompleted()
        {
            this.dataAccess = new DataAccess();
            this.dataAccess.ConnectionString =
                $"Server={ServerName};Database={DatabaseName};Uid={UserID};Pwd={Password};";

            var alarmParams = LoadAlarmParams();
            LoadAlarmItems(alarmParams);
            InitAlarmCondition(alarmParams);

            IsActive = true;
            OnEventConstructionCompleted();
            OnEventConnectionQualityChanged(Quality.Good);
        }

        private IEnumerable<AlarmParam> LoadAlarmParams()
        {
            var query = $"select * from `{TableName}`";
            var dataTable = dataAccess.ExecuteQuery(query);

            return dataTable is null ?
                Enumerable.Empty<AlarmParam>() :
                dataTable?.AsEnumerable()
                    .Select(dataRow => new AlarmParam()
                    {
                        TagName = dataRow.Field<string>("TagName"),
                        TagNo = dataRow.Field<string>("TagNo"),
                        Value = dataRow.Field<string>("Value"),
                        Location = dataRow.Field<string>("Location"),
                        Description = dataRow.Field<string>("Description"),
                        Type = (AlarmType)dataRow.Field<int>("Type"),
                        Level = (AlarmLevel)dataRow.Field<int>("Level"),
                        FaultCode = dataRow.Field<int>("FaultCode")
                    });
        }

        private void LoadAlarmItems(IEnumerable<AlarmParam> alarmParams)
        {
            var query = $"select * from `{TableLog}` order by `OccurrenceTime` desc limit {Limit}";
            var dataTable = dataAccess.ExecuteQuery(query);

            if (dataTable is null) return;
            var alarmList = dataTable.AsEnumerable()
                .Select(dataRow =>
                {
                    var status = dataRow.Field<string>("Status") == "Alarm" ? 
                    AlarmStatus.ALARM : 
                    AlarmStatus.NORMAL;
                    var alarmItem = new AlarmItem()
                    {
                        IsAcknowledge = true,
                        ID = dataRow.Field<string>("ID"),
                        OccurrenceTime = dataRow["OccurrenceTime"] == DBNull.Value ?
                            DateTime.MinValue :
                            dataRow.Field<DateTime>("OccurrenceTime"),
                        RestoreTime = dataRow["RestoreTime"] == DBNull.Value ?
                            DateTime.MinValue :
                            dataRow.Field<DateTime>("RestoreTime"),
                        Status = status
                    };

                    var param = alarmParams.FirstOrDefault(x =>
                        x.TagName == dataRow.Field<string>("TagName") &&
                        x.TagNo == dataRow.Field<string>("TagNo") &&
                        x.Location == dataRow.Field<string>("Location") &&
                        x.Description == dataRow.Field<string>("Description"));
                    if (param != null) alarmItem.Param = param;

                    return alarmItem;
                })
                .Where(x => x.Param != null);

            this.alarmItems.AddRange(alarmList);
        }

        private void InitAlarmCondition(IEnumerable<AlarmParam> alarmParams)
        {
            foreach (var alarmParam in alarmParams)
            {
                var alarmTag = AlarmTagFactory.Get(this.driver, alarmParam);
                if (alarmTag.IsActive)
                {
                    this.alarmTags.Add(alarmTag);
                    alarmTag.StatusChanged += ActionStatusChanged;
                }
            }
        }

        private void ActionStatusChanged(AlarmItem item)
        {
            try
            {
                if (item.Status == AlarmStatus.NORMAL)
                {
                    var inactiveItem = this.alarmItems.FirstOrDefault(x =>
                        x.Param != null && item.Param != null &&
                        string.Equals(x.Param.TagName, item.Param.TagName) &&
                        string.Equals(x.Param.TagNo, item.Param.TagNo) &&
                        x.Status == AlarmStatus.ALARM);

                    if (inactiveItem != null)
                    {
                        inactiveItem.Status = AlarmStatus.NORMAL;
                        inactiveItem.RestoreTime = item.RestoreTime;
                        OnEventPushed(item);

                        dataAccess.ExecuteNonQuery(
                            $"UPDATE `{TableLog}` SET `Status` = @Status, `RestoreTime` = @RestoreTime WHERE `ID` = @ID",
                            "Resolved",
                            item.RestoreTime,
                            inactiveItem.ID);
                        return;
                    }
                }

                var count = this.alarmItems.Count;
                if (Limit > 0 && count >= Limit)
                    this.alarmItems.RemoveAt(count - 1);

                this.alarmItems.Insert(0, item);
                OnEventPushed(item);
            }
            catch { }
            finally
            {

            }
        }

        #endregion

        public AlarmItem[] GetItems(int maxCount)
        {
            try
            {
                return this.alarmItems
                    .Where(x => x != null)
                    .Take(maxCount)
                    .ToArray();
            }
            catch
            {
                return null;
            }
        }

        public void Acknowledge(string[] ids)
        {
            try
            {
                var itemIDAcknowledgeds = new List<string>();
                foreach (var id in ids)
                {
                    var itemAcknowledged = this.alarmItems.Find(x => string.Equals(x.ID, id));
                    if (itemAcknowledged is null) continue;

                    itemIDAcknowledgeds.Add(id);
                    itemAcknowledged.IsAcknowledge = true;
                }
                OnEventAcknowledged(itemIDAcknowledgeds.ToArray());
            }
            catch { }
        }

        public void Reset()
        {
            try
            {
                this.alarmItems.RemoveAll(x => x.Status == AlarmStatus.NORMAL);
                OnEventReseted();
            }
            catch { }
        }

        private void OnEventConstructionCompleted()
        {
            Action handler;
            lock (this) handler = ConstructionCompleted;
            handler?.Invoke();
        }

        private void OnEventConnectionQualityChanged(Quality quality)
        {
            Action<Quality> handler;
            lock (this) handler = ConnectionQualityChanged;
            handler?.Invoke(quality);
        }

        public void OnEventPushed(AlarmItem item)
        {
            try
            {
                mutex.Wait();
               
                Action<AlarmItem> handler;
                lock (this) handler = Pushed;
                handler?.Invoke(item);
            }
            catch { }
            finally
            {
                mutex.Release();
            }

        }

        public void OnEventAcknowledged(string[] ids)
        {
            try
            {
                Action<string[]> handler;
                lock (this) handler = Acknowledged;
                handler?.Invoke(ids);
            }
            catch { }
            finally
            {

            }
        }

        public void OnEventReseted()
        {
            try
            {
                Action handler;
                lock (this) handler = Reseted;
                handler?.Invoke();
            }
            catch { }
            finally
            {

            }
        }
    }
}
