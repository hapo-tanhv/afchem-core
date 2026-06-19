
namespace WindowsFormsApp1
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
            this.alarmHost1 = new HinoTools.Alarm.Host.AlarmHost(this.components);
            this.alarmServer1 = new HinoTools.Alarm.Server.AlarmServer(this.components);
            this.alarmLogger1 = new HinoTools.Alarm.Control.AlarmLogger(this.components);
            this.alarmReportLogger1 = new HinoTools.Data.Log.AlarmReportLogger(this.components);
            this.realtimeThresholdLogger1 = new HinoTools.Data.Log.RealtimeThresholdLogger(this.components);
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
            // alarmHost1
            // 
            this.alarmHost1.AlarmHub = this.alarmServer1;
            this.alarmHost1.Port = ((uint)(9000u));
            this.alarmHost1.Server = "localhost";
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
            // alarmReportLogger1
            // 
            this.alarmReportLogger1.Collection = new string[] {
        "AFChemTX01.QuyTrinh;QuyTrinh",
        "AFChemTX01.CongDoanMay;CongDoanMay",
        "AFChemTX01.ThoiGianCapLieu;ThoiGianCapLieu",
        "AFChemTX01.ThoiGianTron1;ThoiGianTron1",
        "AFChemTX01.ThoiGianXaDay;ThoiGianXaDay",
        "AFChemTX01.ThoiGianHutXaDay;ThoiGianHutXaDay",
        "AFChemTX01.ThoiGianTron2;ThoiGianTron2",
        "AFChemTX01.ThoiGianRungXaDay;ThoiGianRungXaDay",
        "AFChemTX01.ThoiGianXaHang;ThoiGianXaHang",
        "AFChemTX01.ThoiGianRungXaHang;ThoiGianRungXaHang",
        "AFChemTX01.ApSuat;ApSuat",
        "AFChemTX01.NhietDoMoiTruong;NhietDoMoiTruong",
        "AFChemTX01.DoAmMoiTruong;DoAmMoiTruong",
        "AFChemTX01.NhietDoBonTronTren;NhietDoBonTronTren",
        "AFChemTX01.NhietDoBonTronGiua;NhietDoBonTronGiua",
        "AFChemTX01.NhietDoBonTronDuoi;NhietDoBonTronDuoi"};
            this.alarmReportLogger1.DatabaseName = "scada";
            this.alarmReportLogger1.Driver = this.iDriver1;
            this.alarmReportLogger1.HttpPort = 5500;
            this.alarmReportLogger1.Password = "101101";
            this.alarmReportLogger1.PollingInterval = 30000;
            this.alarmReportLogger1.ServerName = "localhost";
            this.alarmReportLogger1.StopTimeout = 7200;
            this.alarmReportLogger1.TableName = "alarmreport";
            this.alarmReportLogger1.UserID = "root";
            this.alarmReportLogger1.WebhookPort = 5605;
            this.alarmReportLogger1.WebhookToken = "wh_tok_2f8d9b1e4c7a6e5b3d2c1f0a9e8d7c6b";
            // 
            // realtimeThresholdLogger1
            // 
            this.realtimeThresholdLogger1.AlarmReportLogger = this.alarmReportLogger1;
            this.realtimeThresholdLogger1.Collection = new string[] {
        "AFChemTX01.NhietDoMoiTruong;NhietDoMoiTruong;45;>;WARNING;Nhiệt độ môi trường vượ" +
            "t ngưỡng cảnh báo",
        "AFChemTX01.ApSuat;ApSuat;60;>;ALARM;Áp suất hệ thống vượt ngưỡng nguy hiểm",
        "AFChemTX01.NhietDoBonTronGiua;NhietDoBonTronGiua;40;>;WARNING;Nhiệt độ bồn trộn g" +
            "iữa vượt ngưỡng cảnh báo",
        "AFChemTX01.DoAmMoiTruong;DoAmMoiTruong;60;>;WARNING;Độ ẩm môi trường vượt ngưỡng " +
            "cảnh báo",
        "AFChemTX01.NhietDoBonTronDuoi;NhietDoBonTronDuoi;40;>;ALARM;Nhiệt độ bồn trộn dướ" +
            "i quá cao",
        "AFChemTX01.NhietDoBonTronTren;NhietDoBonTronTren;40;>;ALARM;Nhiệt độ bồn trộn trê" +
            "n quá cao"};
            this.realtimeThresholdLogger1.DatabaseName = "scada";
            this.realtimeThresholdLogger1.Driver = this.iDriver1;
            this.realtimeThresholdLogger1.Password = "101101";
            this.realtimeThresholdLogger1.ScanInterval = 3000;
            this.realtimeThresholdLogger1.ServerName = "localhost";
            this.realtimeThresholdLogger1.TableName = "realtime_alarms";
            this.realtimeThresholdLogger1.UserID = "root";
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1043, 450);
            this.Name = "Form1";
            this.Text = "Form1";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.ResumeLayout(false);

        }

        #endregion

        private ATSCADA.iDriver iDriver1;
        private HinoTools.Alarm.Host.AlarmHost alarmHost1;
        private HinoTools.Alarm.Server.AlarmServer alarmServer1;
        private HinoTools.Alarm.Control.AlarmLogger alarmLogger1;
        private HinoTools.Data.Log.AlarmReportLogger alarmReportLogger1;
        private HinoTools.Data.Log.RealtimeThresholdLogger realtimeThresholdLogger1;
    }
}

