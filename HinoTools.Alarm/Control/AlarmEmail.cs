using ATSCADA;
using ATSCADA.ToolExtensions.ExtensionMethods;
using HinoTools.Alarm.Email;
using HinoTools.Alarm.Model;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace HinoTools.Alarm.Control
{
    public partial class AlarmEmail : Component
    {
        #region FIELDS

        private ITag emailTag;

        private string body;

        private EmailCore emailCore;

        private IAlarmHub alarmHub;

        private readonly SemaphoreSlim mutex = new SemaphoreSlim(1, 1);

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
        public string Host { get; set; } = "smtp.gmail.com";

        [Category("Hino Settings")]
        public int Port { get; set; } = 587;

        [Category("Hino Settings")]
        public int TimeOut { get; set; } = 10000;

        [Category("Hino Settings")]
        public bool EnableSSL { get; set; } = true;

        [Category("Hino Settings")]
        public string CredentialEmail { get; set; } = "hino.alarm@hino.com";

        [Category("Hino Settings")]
        public string CredentialPassword { get; set; } = "password";

        [Category("Hino Settings")]
        public string Subject { get; set; } = "Hino Alarm";

        [Category("Hino Settings")]
        public string TemplatePath { get; set; } = "~\\Email\\alarm-email-template.html";

        [Category("Hino Settings")]
        public string EmailTagName { get; set; }

        #endregion

        public AlarmEmail()
        {
            InitializeComponent();
        }

        public AlarmEmail(IContainer container)
        {
            container.Add(this);
            InitializeComponent();
        }

        #region METHODS

        private void ActionConstructionCompleted()
        {
            try
            {
                this.emailTag = this.alarmHub.Driver.GetTagByName(EmailTagName);
                if (this.emailTag is null) return;

                var templatePath = GetTemplatePath();
                if (!File.Exists(templatePath)) return;
                using (StreamReader reader = new StreamReader(templatePath))
                {
                    this.body = reader.ReadToEnd();
                }
                this.emailCore = new EmailCore(new EmailParam()
                {
                    Host = Host,
                    Port = Port,
                    TimeOut = TimeOut,
                    EnableSSL = EnableSSL,
                    CredentialEmail = CredentialEmail,
                    CredentialPassword = CredentialPassword
                });
                this.alarmHub.Pushed += SendEmail;
            }
            catch { }
        }

        private void SendEmail(AlarmItem item)
        {
            if (item.Status == AlarmStatus.NORMAL) return;
            try
            {                
                mutex.Wait();
                var emails = this.emailTag.Value
                    ?.Split(';')
                    ?.Select(x => x.Trim())
                    ?.ToArray();
                if (emails is null || emails.Length == 0) return;

                var subject = $"[{item.Param.TagNo}] Alarm Report on {item.OccurrenceTime:dd/MM/yyyy}";
                var bodyMessage = this.body
                    .Replace("{OccurrenceTime}", item.OccurrenceTime.ToString("dd/MM/yyyy HH:mm:ss"))
                    .Replace("{RestoreTime}", item.RestoreTime.ToString("dd/MM/yyyy HH:mm:ss"))
                    .Replace("{Status}", item.Status.ToString())
                    .Replace("{TagName}", item.Param.TagName)
                    .Replace("{TagNo}", item.Param.TagNo)
                    .Replace("{Location}", item.Param.Location)
                    .Replace("{FaultCode}", item.Param.FaultCode.ToString())
                    .Replace("{Description}", item.Param.Description)
                    .Replace("{Value}", item.Param.Value);

                this.emailCore.Send(new EmailContent()
                {                    
                    Subject = subject,
                    Body = bodyMessage,
                    Recipients = emails
                });
            }
            catch { }
            finally
            {
                mutex.Release();
            }
        }

        private string GetTemplatePath()
        {
            if (!TemplatePath.StartsWith("~\\")) return TemplatePath;

            var locationPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            return Path.Combine(locationPath, TemplatePath.Replace("~\\", ""));
        }

        #endregion
    }
}
