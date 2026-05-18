
namespace TestServer
{
    partial class Form1
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.iDriver1 = new ATSCADA.iDriver();
            this.eventViewer1 = new HinoTools.Alarm.Control.EventViewer();
            this.eventServer1 = new HinoTools.Alarm.Server.EventServer(this.components);
            this.alarmServer1 = new HinoTools.Alarm.Server.AlarmServer(this.components);
            this.alarmHost1 = new HinoTools.Alarm.Host.AlarmHost(this.components);
            this.alarmLogger1 = new HinoTools.Alarm.Control.AlarmLogger(this.components);
            this.eventLogger1 = new HinoTools.Alarm.Control.EventLogger(this.components);
            this.eventHost1 = new HinoTools.Alarm.Host.EventHost(this.components);
            this.alarmEmail1 = new HinoTools.Alarm.Control.AlarmEmail(this.components);
            this.alarmViewer1 = new HinoTools.Alarm.Control.AlarmViewer();
            this.SuspendLayout();
            // 
            // iDriver1
            // 
            this.iDriver1.Designmode = false;
            this.iDriver1.GetTaskTimeOut = ((ulong)(5000ul));
            this.iDriver1.MaxTagWriteTimes = 10;
            this.iDriver1.ProjectFile = null;
            this.iDriver1.WaitingTime = 10000;
            // 
            // eventViewer1
            // 
            this.eventViewer1.AlarmHub = this.eventServer1;
            this.eventViewer1.BackColor = System.Drawing.SystemColors.ActiveCaption;
            this.eventViewer1.Limit = 50;
            this.eventViewer1.Location = new System.Drawing.Point(12, 12);
            this.eventViewer1.Name = "eventViewer1";
            this.eventViewer1.Size = new System.Drawing.Size(888, 211);
            this.eventViewer1.TabIndex = 3;
            // 
            // eventServer1
            // 
            this.eventServer1.DatabaseName = "scada";
            this.eventServer1.Driver = null;
            this.eventServer1.Limit = 200;
            this.eventServer1.Password = "101101";
            this.eventServer1.ServerName = "localhost";
            this.eventServer1.TableLog = "eventlog";
            this.eventServer1.TableName = "eventsettings";
            this.eventServer1.UserID = "root";
            // 
            // alarmServer1
            // 
            this.alarmServer1.DatabaseName = "scada";
            this.alarmServer1.Driver = this.iDriver1;
            this.alarmServer1.Limit = 200;
            this.alarmServer1.Password = "101101";
            this.alarmServer1.ServerName = "localhost";
            this.alarmServer1.TableLog = "alarmlog";
            this.alarmServer1.TableName = "alarmsettings";
            this.alarmServer1.UserID = "root";
            // 
            // alarmHost1
            // 
            this.alarmHost1.AlarmHub = this.alarmServer1;
            this.alarmHost1.Port = ((uint)(9000u));
            this.alarmHost1.Server = "localhost";
            // 
            // alarmLogger1
            // 
            this.alarmLogger1.AlarmHub = this.alarmServer1;
            this.alarmLogger1.DatabaseName = "scada";
            this.alarmLogger1.Password = "101101";
            this.alarmLogger1.ServerName = "localhost";
            this.alarmLogger1.TableName = "alarmlog";
            this.alarmLogger1.UserID = "root";
            // 
            // eventLogger1
            // 
            this.eventLogger1.AlarmHub = this.eventServer1;
            this.eventLogger1.DatabaseName = "scada";
            this.eventLogger1.Password = "101101";
            this.eventLogger1.ServerName = "localhost";
            this.eventLogger1.TableName = "eventlog";
            this.eventLogger1.UserID = "root";
            // 
            // eventHost1
            // 
            this.eventHost1.EventHub = this.eventServer1;
            this.eventHost1.Port = ((uint)(9001u));
            this.eventHost1.Server = "localhost";
            // 
            // alarmEmail1
            // 
            this.alarmEmail1.AlarmHub = this.alarmServer1;
            this.alarmEmail1.CredentialEmail = "doandinhvantdh@gmail.com";
            this.alarmEmail1.CredentialPassword = "rmjdkhhojzmtdrxv";
            this.alarmEmail1.EmailTagName = "ColumTest.Tag5";
            this.alarmEmail1.EnableSSL = true;
            this.alarmEmail1.Host = "smtp.gmail.com";
            this.alarmEmail1.Port = 587;
            this.alarmEmail1.Subject = "Hino Alarm";
            this.alarmEmail1.TemplatePath = "~\\Email\\alarm-email-template.html";
            this.alarmEmail1.TimeOut = 10000;
            // 
            // alarmViewer1
            // 
            this.alarmViewer1.AlarmHub = this.alarmServer1;
            this.alarmViewer1.BackColor = System.Drawing.SystemColors.ActiveCaption;
            this.alarmViewer1.Limit = 20;
            this.alarmViewer1.Location = new System.Drawing.Point(12, 229);
            this.alarmViewer1.Name = "alarmViewer1";
            this.alarmViewer1.Size = new System.Drawing.Size(888, 273);
            this.alarmViewer1.TabIndex = 4;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(921, 525);
            this.Controls.Add(this.alarmViewer1);
            this.Controls.Add(this.eventViewer1);
            this.Name = "Form1";
            this.Text = "Server";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.ResumeLayout(false);

        }

        #endregion

        private ATSCADA.iDriver iDriver1;
        private HinoTools.Alarm.Server.AlarmServer alarmServer1;
        private HinoTools.Alarm.Host.AlarmHost alarmHost1;
        private HinoTools.Alarm.Control.AlarmLogger alarmLogger1;
        private HinoTools.Alarm.Server.EventServer eventServer1;
        private HinoTools.Alarm.Control.EventLogger eventLogger1;
        private HinoTools.Alarm.Host.EventHost eventHost1;
        private HinoTools.Alarm.Control.EventViewer eventViewer1;
        private HinoTools.Alarm.Control.AlarmEmail alarmEmail1;
        private HinoTools.Alarm.Control.AlarmViewer alarmViewer1;
    }
}