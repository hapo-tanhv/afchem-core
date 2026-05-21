
namespace HinoTools.Data.Log
{
    partial class AlarmReportLogger
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
            if (disposing && httpServer != null)
            {
                try { httpServer.Stop(); } catch { }
            }
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            if (disposing && tmrLog != null)
            {
                tmrLog.Stop();
                tmrLog.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
        }

        #endregion
    }
}
