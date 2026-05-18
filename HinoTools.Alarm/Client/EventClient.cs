using ATSCADA;
using HinoTools.Alarm.Model;
using HinoTools.Alarm.Model.Event;
using HinoTools.Alarm.Service.Event;
using System;
using System.ComponentModel;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;

namespace HinoTools.Alarm.Client
{
    public partial class EventClient : Component, IEventHub, IEventServiceCallback
    {
        #region FILEDS

        private EventServiceClientBase clientBase;

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
        public uint Port { get; set; } = 9001;        

        [Browsable(false)]
        public bool IsActive { get; private set; }

        [Browsable(false)]
        public Quality ConnectionQuality { get; private set; }

        public event Action ConstructionCompleted;

        public event Action<Quality> ConnectionQualityChanged;

        public event Action<EventItem> Pushed;       

        #endregion
        public EventClient()
        {
            InitializeComponent();
        }

        public EventClient(IContainer container)
        {
            container.Add(this);
            InitializeComponent();
        }

        ~EventClient()
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
                var urlService = $"net.tcp://{Server}:{Port}/EventService";
                var endPoint = new EndpointAddress(urlService);
                var tcpBinding = new NetTcpBinding();
                tcpBinding.TransactionFlow = false;
                tcpBinding.MaxReceivedMessageSize = 2147483647;
                tcpBinding.MaxBufferSize = 2147483647;
                tcpBinding.Security.Transport.ProtectionLevel = System.Net.Security.ProtectionLevel.EncryptAndSign;
                tcpBinding.Security.Transport.ClientCredentialType = TcpClientCredentialType.Windows;
                tcpBinding.Security.Mode = SecurityMode.None;

                this.clientBase = new EventServiceClientBase(new InstanceContext(this), tcpBinding, endPoint);
                this.clientBase.Open();
                this.clientBase.Connect();
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

        public EventItem[] GetItems(int maxCount)
        {
            try
            {
                return this.clientBase?.GetItems(maxCount);
            }
            catch
            {
                return null;
            }           
        }

        public void Push(EventItem eventItem)
        {
            try
            {
                this.clientBase?.Push(eventItem);
            }
            catch { }
        }
       
        public void PushCallback(EventItem item)
        {
            try
            {
                if (item is null) return;
                OnEventPushed(item);
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

        public void OnEventPushed(EventItem item)
        {
            try
            {
                Action<EventItem> handler;
                lock (this) handler = Pushed;
                handler?.Invoke(item);
            }
            catch { }            
        }
    }
}
