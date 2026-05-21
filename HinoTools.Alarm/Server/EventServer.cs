using ATSCADA;
using HinoTools.Alarm.Database;
using HinoTools.Alarm.Model;
using HinoTools.Alarm.Model.Event;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Threading;

namespace HinoTools.Alarm.Server
{
    public partial class EventServer : Component, IEventHub
    {
        #region FILEDS

        private DataAccess dataAccess;

        private readonly List<EventItem> eventItems = new List<EventItem>();

        private readonly List<EventTagBase> eventTags = new List<EventTagBase>();

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
        public string TableName { get; set; } = "eventsettings";

        [Category("Hino Settings")]
        public string TableLog { get; set; } = "eventlog";

        [Category("Hino Settings")]
        public int Limit { get; set; } = 20;

        [Browsable(false)]
        public bool IsActive { get; private set; }

        [Browsable(false)]
        public Quality ConnectionQuality => Quality.Good;

        public event Action ConstructionCompleted;

        public event Action<Quality> ConnectionQualityChanged;

        public event Action<EventItem> Pushed;

        #endregion

        #region CONSTRUCTORS

        public EventServer()
        {
            InitializeComponent();
        }

        public EventServer(IContainer container)
        {
            container.Add(this);
            InitializeComponent();
        }

        #endregion

        #region LOAD

        private void ActionConstructionCompleted()
        {
            this.dataAccess = new DataAccess();
            this.dataAccess.ConnectionString =
                $"Server={ServerName};Database={DatabaseName};Uid={UserID};Pwd={Password};";

            var eventParams = LoadParams();
            LoadEventItems(eventParams);
            InitEventTags(eventParams);

            IsActive = true;           
            OnEventConstructionCompleted();
            OnEventConnectionQualityChanged(Quality.Good);
        }

        private IEnumerable<EventParam> LoadParams()
        {
            var query = $"select * from `{TableName}`";
            var dataTable = this.dataAccess.ExecuteQuery(query);

            return dataTable is null ?
                Enumerable.Empty<EventParam>() :
                dataTable?.AsEnumerable()
                    .Select(dataRow => new EventParam()
                    {
                        TagName = dataRow.Field<string>("TagName"),
                        TagNo = dataRow.Field<string>("TagNo"),
                        Value = dataRow.Field<string>("Value"),
                        ValueActive = dataRow.Field<string>("ValueActive"),
                        ValueInactive = dataRow.Field<string>("ValueInactive"),
                        Location = dataRow.Field<string>("Location"),
                        Description = dataRow.Field<string>("Description"),
                        Type = (EventType)dataRow.Field<int>("Type"),
                        Level = dataRow.Field<int>("Level")
                    });
        }


        private void LoadEventItems(IEnumerable<EventParam> eventParams)
        {
            var query = $"select * from `{TableLog}` order by `OccurrenceTime` desc limit {Limit}";
            var dataTable = dataAccess.ExecuteQuery(query);

            if (dataTable is null) return;
            var eventList = dataTable.AsEnumerable()
                .Select(dataRow =>
                {
                    var eventItem = new EventItem()
                    {                       
                        OccurrenceTime = dataRow["OccurrenceTime"] == DBNull.Value ?
                            DateTime.MinValue :
                            dataRow.Field<DateTime>("OccurrenceTime")                     
                    };

                    eventItem.Status = EventStatus.CUSTOM;
                    eventItem.Param = new EventParam
                    {
                        TagName = dataRow.Field<string>("TagName"),
                        TagNo = dataRow.Field<string>("TagNo"),
                        Location = dataRow.Field<string>("Location"),
                        Description = dataRow.Field<string>("Description")
                    };
                    if (Enum.TryParse(dataRow.Field<string>("Status"), out EventStatus status))
                        eventItem.Status = status;
                    else
                        eventItem.Status = EventStatus.CUSTOM;

                    var param = eventParams.FirstOrDefault(x =>
                      x.TagName == dataRow.Field<string>("TagName") &&
                      x.TagNo == dataRow.Field<string>("TagNo") &&
                      x.Location == dataRow.Field<string>("Location"));

                    if (param != null)
                    {
                        eventItem.Param.Level = param.Level;
                        eventItem.Param.Type = param.Type;
                        eventItem.Param.ValueActive = param.ValueActive;
                        eventItem.Param.ValueInactive = param.ValueInactive;
                    }

                    return eventItem;
                })
                .Where(x => x.Param != null);

            this.eventItems.AddRange(eventList);
        }
       
        private void InitEventTags(IEnumerable<EventParam> eventParams)
        {
            foreach (var param in eventParams)
            {
                var eventTag = EventTagFactory.Get(this.driver, param);
                if (eventTag.IsActive)
                {
                    this.eventTags.Add(eventTag);
                    eventTag.StatusChanged += Push;                        
                }
            }
        }        

        public void Push(EventItem item)
        {
            try
            {
                var count = this.eventItems.Count;
                if (Limit > 0 && count >= Limit)
                    this.eventItems.RemoveAt(count - 1);

                this.eventItems.Insert(0, item);
                OnEventPushed(item);
            }
            catch { }            
        }

        #endregion

        public EventItem[] GetItems(int maxCount)
        {
            try
            {
                return this.eventItems
                    .Where(x => x != null)
                    .Take(maxCount)
                    .ToArray();
            }
            catch
            {
                return null;
            }            
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

        private void OnEventPushed(EventItem item)
        {
            try
            {
                mutex.Wait();

                Action<EventItem> handler;
                lock (this) handler = Pushed;
                handler?.Invoke(item);                
            }
            catch { }
            finally
            {
                mutex.Release();
            }            
        }        
    }
}
