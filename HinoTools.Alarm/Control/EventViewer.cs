using ATSCADA.ToolExtensions.ExtensionMethods;
using HinoTools.Alarm.Model;
using HinoTools.Alarm.Model.Event;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using System.Linq;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace HinoTools.Alarm.Control
{
    public partial class EventViewer : UserControl
    {
        #region CONST

        private const string FormatDateTime = "yyyy/MM/dd HH:mm:ss";

        #endregion

        #region FIELDS 

        private List<EventItem> eventItems;

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
        public int Limit { get; set; } = 20;

        #endregion
        public EventViewer()
        {
            InitializeComponent();
            this.lstvEventItem.UseCompatibleStateImageBehavior = false;
            this.lstvEventItem.GetType()
                .GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(this.lstvEventItem, true, null);
            this.cbxFilter.SelectedIndex = 0;
        }

        private void ActionConstructionCompleted()
        {
            this.eventItems = new List<EventItem>();
            this.eventHub.Pushed += ActionEventPushed;
            this.eventHub.ConnectionQualityChanged += CheckConnectionStatus;
            this.cbxFilter.SelectedIndexChanged += (sender, e) => UpdateListView();
            //CheckConnectionStatus(this.eventHub.ConnectionQuality);
        }

        private void CheckConnectionStatus(Quality quality)
        {
            if (quality == Quality.Good)
            {
                LoadAlarmItems();
                this.SynchronizedInvokeAction(() =>
                {
                    this.lstvEventItem.Enabled = true;
                });
            }
            else
            {
                this.SynchronizedInvokeAction(() =>
                {
                    this.lstvEventItem.Enabled = false;
                });
            }
        }

        private void LoadAlarmItems()
        {
            this.eventItems.AddRange(this.eventHub.GetItems(Limit));
            UpdateListView();
        }

        #region ALARM-HUB EVENT

        private void ActionEventPushed(EventItem item)
        {
            if (item is null) return;
            var count = this.eventItems.Count;
            if (Limit > 0 && count >= Limit)
                this.eventItems.RemoveAt(count - 1);
            this.eventItems.Insert(0, item);
            UpdateListView();
        }

        #endregion

        #region LISTVIEW METHODS

        private void UpdateListView()
        {
            try
            {
                this.lstvEventItem.SynchronizedInvokeAction(() =>
                {
                    var filter = this.cbxFilter.Text;
                    var rangeTime = RangeTimeFactory.Get(filter);
                    var eventItemFilted = this.eventItems
                        .Where(x => x != null && x.OccurrenceTime >= rangeTime.StartTime && x.OccurrenceTime <= rangeTime.EndTime);

                    this.lstvEventItem.BeginUpdate();
                    this.lstvEventItem.Items.Clear();
                    foreach (var eventItem in eventItemFilted)
                    {
                        var index = eventItemFilted.IndexOf(eventItem);
                        var listViewItem = GetListViewItem(eventItem, index);
                        this.lstvEventItem.Items.Add(listViewItem);
                    }
                    this.lstvEventItem.EndUpdate();
                });                     
            }
            catch { }
        }

        private ListViewItem GetListViewItem(EventItem item, int index)
        {
            var status = item.Status == EventStatus.ACTIVE ?
                item.Param.ValueActive :
                item.Param.ValueInactive;
            var listViewItem = new ListViewItem(new string[6]
            {
                 "",
                 item.OccurrenceTime.ToString(FormatDateTime),
                 item.Param.TagNo,
                 item.Param.Location,
                 item.Param.Description,
                 status
            })
            {
                BackColor = GetBackColor(index),
                ForeColor = GetForeColor(item.Param.Level, item.Status)
            };
            listViewItem.SubItems[0].Text = (index + 1).ToString();
            return listViewItem;
        }

        private Color GetBackColor(int level)
        {
            return Color.Black;
        }

        private Color GetForeColor(int level, EventStatus status)
        {
            switch (level)
            {
                case 0:
                    return Color.White;
                case 1:
                    return status == EventStatus.ACTIVE || status == EventStatus.CHANGED ? Color.Yellow : Color.LimeGreen;
                case 2:
                    return status == EventStatus.ACTIVE || status == EventStatus.CHANGED ? Color.Red : Color.LimeGreen;
                default:
                    return Color.White;
            }
        }

        #endregion
    }
}
