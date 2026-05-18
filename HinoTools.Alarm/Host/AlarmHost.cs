using HinoTools.Alarm.Model;
using HinoTools.Alarm.Service;
using System.ComponentModel;
using System.ServiceModel;

namespace HinoTools.Alarm.Host
{
    public partial class AlarmHost : Component
    {
        #region FIELDS 

        private ServiceHost serviceHost = null;

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
        public string Server { get; set; } = "localhost";

        [Category("Hino Settings")]        
        public uint Port { get; set; } = 9000;


        #endregion

        public AlarmHost()
        {
            InitializeComponent();
        }

        public AlarmHost(IContainer container)
        {
            container.Add(this);
            InitializeComponent();
        }

        ~AlarmHost()
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
            var urlService = $"net.tcp://{Server}:{Port}/AlarmService";            
            var tcpBinding = new NetTcpBinding();
            tcpBinding.TransactionFlow = false;
            tcpBinding.MaxReceivedMessageSize = 2147483647;
            tcpBinding.MaxBufferSize = 2147483647;
            tcpBinding.Security.Transport.ProtectionLevel = System.Net.Security.ProtectionLevel.EncryptAndSign;
            tcpBinding.Security.Transport.ClientCredentialType = TcpClientCredentialType.Windows;
            tcpBinding.Security.Mode = SecurityMode.None;

            AlarmServiceDispatcher.Instance.AlarmHub = this.alarmHub;
            this.serviceHost = new ServiceHost(typeof(AlarmServiceHostBase));           
            this.serviceHost.AddServiceEndpoint(typeof(IAlarmService), tcpBinding, urlService);            
            this.serviceHost.Open();
        }
    }
}
