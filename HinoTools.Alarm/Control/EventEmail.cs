using ATSCADA;
using ATSCADA.ToolExtensions.ExtensionMethods;
using HinoTools.Alarm.Email;
using HinoTools.Alarm.Model.Event;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HinoTools.Alarm.Control
{
    public partial class EventEmail : Component
    {
        #region FIELDS

        private ITag emailTag;

        private string body;

        private EmailCore emailCore;

        private IEventHub eventHub;

        private readonly SemaphoreSlim mutex = new SemaphoreSlim(1, 1);

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
        public string Host { get; set; } = "smtp.gmail.com";

        [Category("Hino Settings")]
        public int Port { get; set; } = 587;

        [Category("Hino Settings")]
        public int TimeOut { get; set; } = 10000;

        [Category("Hino Settings")]
        public bool EnableSSL { get; set; } = false;

        [Category("Hino Settings")]
        public string CredentialEmail { get; set; } = "hino.alarm@hino.com";

        [Category("Hino Settings")]
        public string CredentialPassword { get; set; } = "password";

        [Category("Hino Settings")]
        public string Subject { get; set; } = "Hino Alarm";

        [Category("Hino Settings")]
        public string TemplatePath { get; set; } = "~\\Email\\event-email-template.html";

        [Category("Hino Settings")]
        public string EmailTagName { get; set; }

        #endregion

        public EventEmail()
        {
            InitializeComponent();
        }

        public EventEmail(IContainer container)
        {
            container.Add(this);

            InitializeComponent();
        }

        #region 

        private void ActionConstructionCompleted()
        {
            try
            {
                this.emailTag = this.eventHub.Driver.GetTagByName(EmailTagName);
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
                this.eventHub.Pushed += SendEmail;
            }
            catch { }
        }

        public void SendEmail(EventItem item)
        {            
            try
            {
                mutex.Wait();
                var emails = this.emailTag.Value
                    ?.Split(';')
                    ?.Select(x => x.Trim())
                    ?.ToArray();
                if (emails is null || emails.Length == 0) return;

                var status = item.Status == EventStatus.ACTIVE ? item.Param.ValueActive : item.Param.ValueInactive;
                var bodyMessage = this.body
                    .Replace("{OccurrenceTime}", item.OccurrenceTime.ToString("dd/MM/yyyy HH:mm:ss"))                    
                    .Replace("{Status}", status)
                    .Replace("{TagName}", item.Param.TagName)
                    .Replace("{TagNo}", item.Param.TagNo)
                    .Replace("{Location}", item.Param.Location)
                    .Replace("{Description}", item.Param.Description)
                    .Replace("{Value}", item.Param.Value);

                this.emailCore.Send(new EmailContent()
                {
                    Subject = Subject,
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
