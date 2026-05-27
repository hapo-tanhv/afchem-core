using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WindowsFormsApp1
{
    public partial class Form1 : Form
    {
        private NotifyIcon sysTrayIcon;
        private ContextMenuStrip trayMenu;
        private bool isExplicitClose = false;

        public Form1()
        {
            InitializeComponent();
            InitializeSystemTray();
        }

        private void InitializeSystemTray()
        {
            trayMenu = new ContextMenuStrip();
            
            var showMenuItem = new ToolStripMenuItem("Hiển thị giao diện");
            showMenuItem.Click += (s, e) => {
                this.Show();
                this.WindowState = FormWindowState.Normal;
                this.Activate();
            };
            trayMenu.Items.Add(showMenuItem);
            
            trayMenu.Items.Add(new ToolStripSeparator());
            
            var exitMenuItem = new ToolStripMenuItem("Thoát hoàn toàn (Admin)");
            exitMenuItem.Click += MenuExit_Click;
            trayMenu.Items.Add(exitMenuItem);

            sysTrayIcon = new NotifyIcon();
            sysTrayIcon.Text = "HinoTools Alarm Service - Chạy ngầm";
            sysTrayIcon.Icon = SystemIcons.Application; // Sử dụng icon hệ thống mặc định
            sysTrayIcon.ContextMenuStrip = trayMenu;
            sysTrayIcon.Visible = true;

            // Double click để mở lại giao diện nhanh chóng
            sysTrayIcon.DoubleClick += (s, e) => {
                this.Show();
                this.WindowState = FormWindowState.Normal;
                this.Activate();
            };
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Nếu người dùng bấm X hoặc Alt+F4
            if (e.CloseReason == CloseReason.UserClosing && !isExplicitClose)
            {
                e.Cancel = true; // Chặn đóng ứng dụng
                this.Hide();     // Ẩn Form chính

                sysTrayIcon.ShowBalloonTip(3000, 
                    "Hệ thống đang chạy ngầm", 
                    "Ứng dụng HinoTools.Alarm vẫn tiếp tục thu thập dữ liệu và chạy ngầm ở khay hệ thống.", 
                    ToolTipIcon.Info);
            }
            else
            {
                // Giải phóng NotifyIcon khi đóng ứng dụng hoàn toàn (ví dụ: Windows Shutdown hoặc Admin exit)
                if (sysTrayIcon != null)
                {
                    sysTrayIcon.Visible = false;
                    sysTrayIcon.Dispose();
                }
                base.OnFormClosing(e);
            }
        }

        private void MenuExit_Click(object sender, EventArgs e)
        {
            var confirmResult = MessageBox.Show(
                "Bạn có chắc chắn muốn tắt hoàn toàn hệ thống cảnh báo HinoTools? Hành động này sẽ dừng việc thu thập dữ liệu!",
                "Xác nhận thoát hệ thống",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirmResult == DialogResult.Yes)
            {
                isExplicitClose = true;
                Application.Exit();
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }
    }
}
