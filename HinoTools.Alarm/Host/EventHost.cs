using HinoTools.Alarm.Model.Event;
using HinoTools.Alarm.Service.Event;
using System.ComponentModel;
using System.ServiceModel;

namespace HinoTools.Alarm.Host
{
    public partial class EventHost : Component
    {
        #region FIELDS 

        private ServiceHost serviceHost = null;

        private IEventHub eventHub;

        #endregion

        #region PROPERTIES


        [Category("Hino Settings")]
        public IEventHub EventHub
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
        public string Server { get; set; } = "localhost";

        [Category("Hino Settings")]
        public uint Port { get; set; } = 9001;


        #endregion

        public EventHost()
        {
            InitializeComponent();
        }

        public EventHost(IContainer container)
        {
            container.Add(this);
            InitializeComponent();
        }

        ~EventHost()
        {
            StopService();
        }

        private void StopService()
        {
            try
            {
                this.serviceHost?.Close();
                this.serviceHost?.Abort();
            }
            catch { }
        }

        private void ActionConstructionCompleted()
        {
            var urlService = $"net.tcp://{Server}:{Port}/EventService";
            var tcpBinding = new NetTcpBinding();
            tcpBinding.TransactionFlow = false;
            tcpBinding.MaxReceivedMessageSize = 2147483647;
            tcpBinding.MaxBufferSize = 2147483647;
            tcpBinding.Security.Transport.ProtectionLevel = System.Net.Security.ProtectionLevel.EncryptAndSign;
            tcpBinding.Security.Transport.ClientCredentialType = TcpClientCredentialType.Windows;
            tcpBinding.Security.Mode = SecurityMode.None;

            EventServiceDispatcher.Instance.EventHub = this.eventHub;
            this.serviceHost = new ServiceHost(typeof(EventServiceHostBase));
            this.serviceHost.AddServiceEndpoint(typeof(IEventService), tcpBinding, urlService);
            this.serviceHost.Open();
        }
    }
}
