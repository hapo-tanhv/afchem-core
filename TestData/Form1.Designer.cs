
namespace TestData
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
            this.alarmReportLogger1 = new HinoTools.Data.Log.AlarmReportLogger(this.components);
            this.realtimeThresholdLogger1 = new HinoTools.Data.Log.RealtimeThresholdLogger(this.components);
            this.alarmServer1 = new HinoTools.Alarm.Server.AlarmServer(this.components);
            this.alarmLogger1 = new HinoTools.Alarm.Control.AlarmLogger(this.components);
            this.iLabel1 = new ATSCADA.iWinTools.Data.iLabel();
            this.alarmHost1 = new HinoTools.Alarm.Host.AlarmHost(this.components);
            this.SuspendLayout();
            // 
            // iDriver1
            // 
            this.iDriver1.Designmode = false;
            this.iDriver1.GetTaskTimeOut = ((ulong)(5000ul));
            this.iDriver1.MaxTagWriteTimes = 10;
            this.iDriver1.ProjectFile = null;
            this.iDriver1.WaitingTime = 3000;
            // 
            // alarmReportLogger1
            // 
            this.alarmReportLogger1.Collection = new string[] {
        "AFChemTX01.NhietDoMay;NhietDoMay",
        "AFChemTX01.NhietDoMoiTruong;NhietDoMoiTruong",
        "AFChemTX01.ApSuat;ApSuat",
        "AFChemTX01.QuyTrinh;QuyTrinh",
        "AFChemTX01.CongDoanMay;CongDoanMay",
        "AFChemTX01.NhietDoGiuaBuongTron;NhietDoGiuaBuongTron",
        "AFChemTX01.DoAmMoiTruong;DoAmMoiTruong",
        "AFChemTX01.ThoiGianCapLieu;ThoiGianCapLieu",
        "AFChemTX01.ThoiGianTron1;ThoiGianTron1",
        "AFChemTX01.ThoiGianXaDay;ThoiGianXaDay",
        "AFChemTX01.ThoiGianHutXaDay;ThoiGianHutXaDay",
        "AFChemTX01.ThoiGianTron2;ThoiGianTron2",
        "AFChemTX01.ThoiGianRungXaDay;ThoiGianRungXaDay",
        "AFChemTX01.ThoiGianXaHang;ThoiGianXaHang",
        "AFChemTX01.ThoiGianRungXaHang;ThoiGianRungXaHang",
        "AFChemTX01.NhietDoDayBuongTron;NhietDoDayBuongTron",
        "AFChemTX01.ThoiGianDongCoTron;ThoiGianDongCoTron",
        "AFChemTX01.NhietDoNapBuongTron;NhietDoNapBuongTron"};
            this.alarmReportLogger1.DatabaseName = "scada";
            this.alarmReportLogger1.Driver = this.iDriver1;
            this.alarmReportLogger1.Password = "101101";
            this.alarmReportLogger1.PollingInterval = 30000;
            this.alarmReportLogger1.ServerName = "localhost";
            this.alarmReportLogger1.TableName = "alarmreport";
            this.alarmReportLogger1.UserID = "root";
            // 
            // realtimeThresholdLogger1
            // 
            this.realtimeThresholdLogger1.AlarmReportLogger = this.alarmReportLogger1;
            this.realtimeThresholdLogger1.Collection = new string[] {
        "AFChemTX01.NhietDoMay;NhietDoMay;50;>;HIGH;Nhiệt độ máy vượt ngưỡng nguy hiểm",
        "AFChemTX01.ApSuat;ApSuat;10;<;AVERAGE;Áp suất hệ thống thấp hơn mức an toàn"};
            this.realtimeThresholdLogger1.DatabaseName = "scada";
            this.realtimeThresholdLogger1.Driver = this.iDriver1;
            this.realtimeThresholdLogger1.Password = "101101";
            this.realtimeThresholdLogger1.ScanInterval = 3000;
            this.realtimeThresholdLogger1.ServerName = "localhost";
            this.realtimeThresholdLogger1.TableName = "realtime_alarms";
            this.realtimeThresholdLogger1.UserID = "root";
            // 
            // alarmServer1
            // 
            this.alarmServer1.DatabaseName = "scada";
            this.alarmServer1.Driver = this.iDriver1;
            this.alarmServer1.Limit = 20;
            this.alarmServer1.Password = "101101";
            this.alarmServer1.ServerName = "localhost";
            this.alarmServer1.TableLog = "alarmlog";
            this.alarmServer1.TableName = "alarmsettings";
            this.alarmServer1.UserID = "root";
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
            // iLabel1
            // 
            this.iLabel1.AutoSize = true;
            this.iLabel1.Driver = this.iDriver1;
            this.iLabel1.Location = new System.Drawing.Point(467, 126);
            this.iLabel1.Name = "iLabel1";
            this.iLabel1.Size = new System.Drawing.Size(41, 13);
            this.iLabel1.TabIndex = 0;
            this.iLabel1.TagName = "AFChemTX01.ThoiGianCapLieu";
            this.iLabel1.Text = "iLabel1";
            // 
            // alarmHost1
            // 
            this.alarmHost1.AlarmHub = this.alarmServer1;
            this.alarmHost1.Port = ((uint)(9000u));
            this.alarmHost1.Server = "localhost";
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.iLabel1);
            this.Name = "Form1";
            this.Text = "Form1";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private ATSCADA.iDriver iDriver1;
        private HinoTools.Data.Log.AlarmReportLogger alarmReportLogger1;
        private HinoTools.Data.Log.RealtimeThresholdLogger realtimeThresholdLogger1;
        private HinoTools.Alarm.Server.AlarmServer alarmServer1;
        private HinoTools.Alarm.Control.AlarmLogger alarmLogger1;
        private ATSCADA.iWinTools.Data.iLabel iLabel1;
        private HinoTools.Alarm.Host.AlarmHost alarmHost1;
    }
}

