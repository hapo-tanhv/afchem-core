using ATSCADA.ToolExtensions.ExtensionMethods;
using HinoTools.Alarm.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace HinoTools.Alarm.Control
{
    public partial class AlarmViewer : UserControl
    {
        #region CONST

        private const string FormatDateTime = "yyyy/MM/dd HH:mm:ss";

        #endregion

        #region FIELDS 

        private bool isFlashOn;

        private List<AlarmItem> alarmItems;

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
        public int Limit { get; set; } = 20;

        #endregion

        public AlarmViewer()
        {
            InitializeComponent();
            this.lstvAlarmItem.UseCompatibleStateImageBehavior = false;
            this.lstvAlarmItem.GetType()
                .GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(this.lstvAlarmItem, true, null);
            this.cbxFilter.SelectedIndex = 0;

        }
        protected override void OnNotifyMessage(Message m)
        {
            if (m.Msg != 0x14)
            {
                base.OnNotifyMessage(m);
            }
        }

        private void ActionConstructionCompleted()
        {
            this.alarmItems = new List<AlarmItem>();
            this.btnAck.Click += OnEventBtnAckClicked;
            this.btnAckAll.Click += OnEventBtnAckAllClicked;
            this.btnReset.Click += OnEventBtnResetClicked;

            this.alarmHub.Pushed += ActionEventPushed;
            this.alarmHub.Acknowledged += ActionEventAckowledge;
            this.alarmHub.Reseted += ActionEventReseted;
            this.alarmHub.ConnectionQualityChanged += CheckConnectionStatus;

            this.tmrFlash.Tick += TmrFlashTick;
            this.cbxFilter.SelectedIndexChanged += (sender, e) => UpdateListView();
            //CheckConnectionStatus(this.alarmHub.ConnectionQuality);
        }

        private void CheckConnectionStatus(Quality quality)
        {
            if (quality == Quality.Good)
            {
                LoadAlarmItems();
                this.SynchronizedInvokeAction(() =>
                {
                    this.btnAck.Enabled = true;
                    this.btnAckAll.Enabled = true;
                    this.btnReset.Enabled = true;
                    this.lstvAlarmItem.Enabled = true;

                    this.tmrFlash.Enabled = HasAlarmActiveNotAck();
                    this.tmrFlash.Start();
                });
            }
            else
            {
                this.SynchronizedInvokeAction(() =>
                {
                    this.btnAck.Enabled = false;
                    this.btnAckAll.Enabled = false;
                    this.btnReset.Enabled = false;
                    this.lstvAlarmItem.Enabled = false;

                    this.tmrFlash.Enabled = false;
                    SetFlashOff();
                });
            }
        }

        private void LoadAlarmItems()
        {
            this.alarmItems.Clear();
            this.alarmItems.AddRange(this.alarmHub.GetItems(Limit));
            if (this.alarmItems.Count == 0)
            {
                this.lstvAlarmItem.SynchronizedInvokeAction(() =>
                {
                    this.lstvAlarmItem.BeginUpdate();
                    this.lstvAlarmItem.Items.Clear();
                    this.lstvAlarmItem.EndUpdate();
                });
                return;
            }
            var listViewItems = new List<ListViewItem>();
            foreach (var alarmItem in this.alarmItems)
            {
                var noNumber = this.alarmItems.IndexOf(alarmItem) + 1;
                listViewItems.Add(GetListViewItem(alarmItem, noNumber));
            }
            this.lstvAlarmItem.SynchronizedInvokeAction(() =>
            {
                this.lstvAlarmItem.BeginUpdate();
                this.lstvAlarmItem.Items.Clear();
                this.lstvAlarmItem.Items.AddRange(listViewItems.ToArray());                
                this.lstvAlarmItem.EndUpdate();
            });
        }

        #region PUBLIC METHOD

        public void SetEnable(bool enable)
        {
            this.btnAck.Enabled = enable;
            this.btnAckAll.Enabled = enable;
            this.btnReset.Enabled = enable;
        }

        #endregion

        #region BUTTON EVENT

        private void OnEventBtnAckClicked(object sender, EventArgs e)
        {
            if (this.lstvAlarmItem.SelectedItems is null ||
                this.lstvAlarmItem.SelectedItems.Count < 1) return;
            var selectedItem = this.lstvAlarmItem.SelectedItems[0];
            if (selectedItem is null) return;
            if (selectedItem.Tag is AlarmItem alarmItem)
            {
                this.alarmHub.Acknowledge(new string[1] { alarmItem.ID });
            }
        }

        private void OnEventBtnAckAllClicked(object sender, EventArgs e)
        {
            var ids = this.lstvAlarmItem.Items.Cast<ListViewItem>()
                .Where(x => x.Tag is AlarmItem alarmItem && !alarmItem.IsAcknowledge)
                .Select(x => (x.Tag as AlarmItem).ID)
                .ToArray();

            this.alarmHub.Acknowledge(ids);
        }

        private void OnEventBtnResetClicked(object sender, EventArgs e)
        {
            this.alarmHub.Reset();
        }

        #endregion

        #region ALARM-HUB EVENT

        private void ActionEventPushed(AlarmItem alarmItem)
        {
            if (alarmItem is null) return;
            if (alarmItem.Status == AlarmStatus.ALARM)
            {
                var count = this.alarmItems.Count;
                if (Limit > 0 && count >= Limit)
                    this.alarmItems.RemoveAt(count - 1);
                this.alarmItems.Insert(0, alarmItem);

                UpdateListView();
                return;
            }

            this.lstvAlarmItem.SynchronizedInvokeAction(() =>
            {
                var listViewItem = this.lstvAlarmItem.Items.Cast<ListViewItem>()
                .FirstOrDefault(x => x.Tag is AlarmItem item && item.ID == alarmItem.ID);
                if (listViewItem is null)
                {
                    var count = this.alarmItems.Count;
                    if (Limit > 0 && count >= Limit)
                        this.alarmItems.RemoveAt(count - 1);
                    this.alarmItems.Insert(0, alarmItem);

                    UpdateListView();
                    return;
                }

                this.lstvAlarmItem.BeginUpdate();
                listViewItem.BackColor = Color.LimeGreen;
                listViewItem.ForeColor = Color.Black;
                listViewItem.SubItems[2].Text = alarmItem.RestoreTime.ToString(FormatDateTime);
                listViewItem.SubItems[7].Text = "Resolved";
                this.tmrFlash.Enabled = HasAlarmActiveNotAck();
                this.lstvAlarmItem.EndUpdate();
            });
        }

        private void ActionEventAckowledge(string[] ids)
        {
            this.lstvAlarmItem.SynchronizedInvokeAction(() =>
            {
                this.lstvAlarmItem.BeginUpdate();
                foreach (ListViewItem listViewItem in this.lstvAlarmItem.Items)
                {
                    if (listViewItem.Tag is AlarmItem alarmItem)
                    {
                        if (ids.Contains(alarmItem.ID))
                        {
                            alarmItem.IsAcknowledge = true;
                            if (alarmItem.Status == AlarmStatus.ALARM)
                            {
                                if (alarmItem.Param.Level == AlarmLevel.High)
                                {
                                    listViewItem.BackColor = Color.Red;
                                    listViewItem.ForeColor = Color.Black;
                                }
                                else
                                {
                                    listViewItem.BackColor = Color.Yellow;
                                    listViewItem.ForeColor = Color.Black;
                                }
                            }
                        }
                    }
                }
                this.tmrFlash.Enabled = HasAlarmActiveNotAck();
                this.lstvAlarmItem.EndUpdate();
            });
        }

        private void ActionEventReseted()
        {
            this.alarmItems = this.alarmItems.Where(x => x.Status != AlarmStatus.NORMAL).ToList();
            UpdateListView();
        }

        #endregion

        #region LISTVIEW METHODS


        private void UpdateListView()
        {
            this.lstvAlarmItem.SynchronizedInvokeAction(() =>
            {
                var filter = this.cbxFilter.Text;
                var rangeTime = RangeTimeFactory.Get(filter);
                var alarmItemFilted = this.alarmItems
                    .Where(x => x != null && x.OccurrenceTime >= rangeTime.StartTime && x.OccurrenceTime <= rangeTime.EndTime);

                this.lstvAlarmItem.BeginUpdate();
                this.lstvAlarmItem.Items.Clear();
                foreach (var alarmItem in alarmItemFilted)
                {
                    var noNumber = alarmItemFilted.IndexOf(alarmItem) + 1;
                    var listViewItem = GetListViewItem(alarmItem, noNumber);                    
                    this.lstvAlarmItem.Items.Add(listViewItem);
                }
                this.tmrFlash.Enabled = HasAlarmActiveNotAck();
                this.lstvAlarmItem.EndUpdate();
            });
        }


        private ListViewItem GetListViewItem(AlarmItem alarmItem, int number)
        {
            var occurrenceTime = alarmItem.OccurrenceTime == DateTime.MinValue ?
                "- - -" : alarmItem.OccurrenceTime.ToString(FormatDateTime);
            var restoreTime = alarmItem.RestoreTime == DateTime.MinValue ?
                "- - -" : alarmItem.RestoreTime.ToString(FormatDateTime);
            var status = alarmItem.Status == AlarmStatus.ALARM ? "Alarm" : "Resolved";
            var backColor = alarmItem.Status == AlarmStatus.NORMAL ? Color.LimeGreen :
                alarmItem.Param.Level == AlarmLevel.High ? Color.Red : Color.Yellow;
            var foreColor = Color.Black;
            return new ListViewItem(new string[8]
            {
                 number.ToString(),
                 occurrenceTime,
                 restoreTime,
                 alarmItem.Param.TagNo,
                 alarmItem.Param.Location,
                 alarmItem.Param.FaultCode.ToString(),
                 alarmItem.Param.Description,
                 status                 
            })
            {
                BackColor = backColor,
                ForeColor = foreColor,
                Tag = alarmItem
            };
        }



        #endregion

        #region FLASH

        private void TmrFlashTick(object sender, EventArgs e)
        {
            if (this.isFlashOn) SetFlashOn();
            else SetFlashOff();
            this.isFlashOn = !this.isFlashOn;
        }

        private void SetFlashOn()
        {
            this.lstvAlarmItem.BeginUpdate();
            foreach (ListViewItem listViewItem in this.lstvAlarmItem.Items)
            {
                if (listViewItem.Tag is AlarmItem alarmItem)
                {
                    if (alarmItem.Status == AlarmStatus.ALARM &&
                        !alarmItem.IsAcknowledge)
                    {
                        if (alarmItem.Param.Level == AlarmLevel.High)
                        {
                            listViewItem.BackColor = Color.Black;
                            listViewItem.ForeColor = Color.Red;
                        }
                        else
                        {
                            listViewItem.BackColor = Color.Black;
                            listViewItem.ForeColor = Color.Yellow;
                        }
                    }
                }
            }
            this.lstvAlarmItem.EndUpdate();
        }

        private void SetFlashOff()
        {
            this.lstvAlarmItem.BeginUpdate();
            foreach (ListViewItem listViewItem in this.lstvAlarmItem.Items)
            {
                if (listViewItem.Tag is AlarmItem alarmItem)
                {
                    if (alarmItem.Status == AlarmStatus.ALARM)
                    {
                        if (alarmItem.Param.Level == AlarmLevel.High)
                        {
                            listViewItem.BackColor = Color.Red;
                            listViewItem.ForeColor = Color.Black;
                        }
                        else
                        {
                            listViewItem.BackColor = Color.Yellow;
                            listViewItem.ForeColor = Color.Black;
                        }
                    }
                }
            }
            this.lstvAlarmItem.EndUpdate();
        }

        private bool HasAlarmActiveNotAck()
        {
            return this.lstvAlarmItem.Items.Cast<ListViewItem>()
                .Any(x => x.Tag is AlarmItem item && item.Status == AlarmStatus.ALARM && !item.IsAcknowledge);
        }

        #endregion
    }
}
