using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WindowsFormsApp1
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // Chặn mở 2 bản chương trình cùng lúc trên một máy: bản thứ hai thoát và ghi log.
            bool createdNew;
            using (var singleInstance = new System.Threading.Mutex(true, @"Global\HinoTools.Scada.WindowsFormsApp1", out createdNew))
            {
                if (!createdNew)
                {
                    try
                    {
                        string logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? "", "scada_runtime.log");
                        System.IO.File.AppendAllText(logPath,
                            string.Format("[{0:yyyy-MM-dd HH:mm:ss}] SKIP chuong trinh da chay, ban thu hai thoat.{1}", DateTime.Now, Environment.NewLine));
                    }
                    catch { }
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new Form1());
            }
        }
    }
}
