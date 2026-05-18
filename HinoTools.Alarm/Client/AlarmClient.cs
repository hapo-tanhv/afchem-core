using ATSCADA;
using HinoTools.Alarm.Model;
using HinoTools.Alarm.Service;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;

namespace HinoTools.Alarm.Client
{
    public partial class AlarmClient : Component, IAlarmHub, IAlarmServiceCallback
    {
        #region FILEDS

        private List<AlarmItem> alarmItems = new List<AlarmItem>();

        private AlarmServiceClientBase clientBase;

        private iDriver driver;

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
        public string Server { get; set; } = "localhost";

        [Category("Hino Settings")]
        public uint Port { get; set; } = 9000;

        [Category("Hino Settings")]
        public int Limit { get; set; } = 20;

        [Browsable(false)]
        public int Count => this.alarmItems.Where(x => x.Status == AlarmStatus.ALARM).Count();

        [Browsable(false)]
        public bool IsActive { get; private set; }

        [Browsable(false)]
        public Quality ConnectionQuality { get; private set; }

        public event Action ConstructionCompleted;

        public event Action<Quality> ConnectionQualityChanged;

        public event Action<AlarmItem> Pushed;

        public event Action<string[]> Acknowledged;

        public event Action Reseted;

        #endregion

        public AlarmClient()
        {
            InitializeComponent();
        }

        public AlarmClient(IContainer container)
        {
            container.Add(this);
            InitializeComponent();
        }

        ~AlarmClient()
        {
            Disconnect();
        }

        private void ActionConstructionCompleted()
        {
            Connect();
            CheckAlive();
            IsActive = true;
            OnEventConstructionCompleted();
        }

        private void CheckAlive()
        {
            System.Threading.Tasks.Task.Factory.StartNew(
                new Action(PingToServer),
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        private void PingToServer()
        {
            while (true)
            {
                try
                {
                    var quality = this.clientBase.Ping() ? Quality.Good : Quality.Bad;
                    if (ConnectionQuality != quality)
                    {
                        ConnectionQuality = quality;
                        OnEventConnectionQualityChanged(quality);
                    }
                }
                catch
                {
                    if (ConnectionQuality != Quality.Bad)
                    {
                        ConnectionQuality = Quality.Bad;
                        OnEventConnectionQualityChanged(Quality.Bad);
                    }
                    Connect();
                }
                finally
                {
                    Thread.Sleep(5000);
                }
            }
        }

        private void Connect()
        {
            try
            {
                var urlService = $"net.tcp://{Server}:{Port}/AlarmService";
                var endPoint = new EndpointAddress(urlService);
                var tcpBinding = new NetTcpBinding();
                tcpBinding.TransactionFlow = false;
                tcpBinding.MaxReceivedMessageSize = 2147483647;
                tcpBinding.MaxBufferSize = 2147483647;
                tcpBinding.Security.Transport.ProtectionLevel = System.Net.Security.ProtectionLevel.EncryptAndSign;
                tcpBinding.Security.Transport.ClientCredentialType = TcpClientCredentialType.Windows;
                tcpBinding.Security.Mode = SecurityMode.None;

                this.clientBase = new AlarmServiceClientBase(new InstanceContext(this), tcpBinding, endPoint);
                this.clientBase.Open();
                this.clientBase.Connect();

                LoadAlarmItems();
            }
            catch { }
        }

        private void Disconnect()
        {
            try
            {
                this.clientBase?.Disconnect();
                this.clientBase?.Close();
                this.clientBase?.Abort();
            }
            catch { }
        }

        private void LoadAlarmItems()
        {
            try
            {
                this.alarmItems = this.clientBase.GetItems(Limit).ToList();
            }
            catch { }            
        }

        public AlarmItem[] GetItems(int maxCount)
        {
            try
            {
                return this.alarmItems
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
                this.clientBase?.Acknowledge(ids);
            }
            catch { }            
        }

        public void Reset()
        {
            try
            {
                this.clientBase?.Reset();
            }
            catch { }            
        }

        public void PushCallback(AlarmItem item)
        {
            try
            {
                if (item is null) return;
                if (item.Status == AlarmStatus.NORMAL)
                {
                    var inactiveItem = this.alarmItems.FirstOrDefault(x => string.Equals(x.ID, item.ID));
                    if (inactiveItem != null)
                    {
                        inactiveItem.Status = AlarmStatus.NORMAL;
                        inactiveItem.RestoreTime = item.RestoreTime;
                        OnEventPushed(item);
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
        }

        public void AcknowledgeCallback(string[] ids)
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
                OnEventAcknowledged(ids);
            }
            catch { }
            
        }

        public void ResetCallback()
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
            try
            {
                Action handler;
                lock (this) handler = ConstructionCompleted;
                handler?.Invoke();
            }
            catch { }
            
        }

        private void OnEventConnectionQualityChanged(Quality quality)
        {
            try
            {
                Action<Quality> handler;
                lock (this) handler = ConnectionQualityChanged;
                handler?.Invoke(quality);
            }
            catch { }
            
        }

        public void OnEventPushed(AlarmItem item)
        {
            try
            {
                Action<AlarmItem> handler;
                lock (this) handler = Pushed;
                handler?.Invoke(item);
            }
            catch { }           
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
        }
    }
}
