
namespace HinoTools.Alarm.Control
{
    partial class AlarmViewer
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.lstvAlarmItem = new System.Windows.Forms.ListView();
            this.colNo = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colOccurrenceTime = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colRestoreTime = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colTagNo = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colLocation = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colFaultCode = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colDescription = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colStatus = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.btnAck = new System.Windows.Forms.Button();
            this.btnReset = new System.Windows.Forms.Button();
            this.btnAckAll = new System.Windows.Forms.Button();
            this.tmrFlash = new System.Windows.Forms.Timer(this.components);
            this.label1 = new System.Windows.Forms.Label();
            this.cbxFilter = new System.Windows.Forms.ComboBox();
            this.SuspendLayout();
            // 
            // lstvAlarmItem
            // 
            this.lstvAlarmItem.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lstvAlarmItem.BackColor = System.Drawing.Color.White;
            this.lstvAlarmItem.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colNo,
            this.colOccurrenceTime,
            this.colRestoreTime,
            this.colTagNo,
            this.colLocation,
            this.colFaultCode,
            this.colDescription,
            this.colStatus});
            this.lstvAlarmItem.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lstvAlarmItem.ForeColor = System.Drawing.Color.Black;
            this.lstvAlarmItem.FullRowSelect = true;
            this.lstvAlarmItem.GridLines = true;
            this.lstvAlarmItem.HideSelection = false;
            this.lstvAlarmItem.Location = new System.Drawing.Point(14, 49);
            this.lstvAlarmItem.MultiSelect = false;
            this.lstvAlarmItem.Name = "lstvAlarmItem";
            this.lstvAlarmItem.Size = new System.Drawing.Size(864, 178);
            this.lstvAlarmItem.TabIndex = 0;
            this.lstvAlarmItem.UseCompatibleStateImageBehavior = false;
            this.lstvAlarmItem.View = System.Windows.Forms.View.Details;
            // 
            // colNo
            // 
            this.colNo.Text = "No.";
            this.colNo.Width = 35;
            // 
            // colOccurrenceTime
            // 
            this.colOccurrenceTime.Text = "Occurrence Time";
            this.colOccurrenceTime.Width = 130;
            // 
            // colRestoreTime
            // 
            this.colRestoreTime.Text = "Restore Time";
            this.colRestoreTime.Width = 130;
            // 
            // colTagNo
            // 
            this.colTagNo.Text = "Plant Name";
            this.colTagNo.Width = 130;
            // 
            // colLocation
            // 
            this.colLocation.Text = "Device";
            this.colLocation.Width = 120;
            // 
            // colFaultCode
            // 
            this.colFaultCode.Text = "Fault Code";
            this.colFaultCode.Width = 100;
            // 
            // colDescription
            // 
            this.colDescription.Text = "Description";
            this.colDescription.Width = 150;
            // 
            // colStatus
            // 
            this.colStatus.Text = "Status";
            // 
            // btnAck
            // 
            this.btnAck.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnAck.BackColor = System.Drawing.SystemColors.Control;
            this.btnAck.Location = new System.Drawing.Point(641, 237);
            this.btnAck.Name = "btnAck";
            this.btnAck.Size = new System.Drawing.Size(75, 23);
            this.btnAck.TabIndex = 1;
            this.btnAck.Text = "Ack";
            this.btnAck.UseVisualStyleBackColor = false;
            // 
            // btnReset
            // 
            this.btnReset.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnReset.Location = new System.Drawing.Point(803, 237);
            this.btnReset.Name = "btnReset";
            this.btnReset.Size = new System.Drawing.Size(75, 23);
            this.btnReset.TabIndex = 2;
            this.btnReset.Text = "Reset";
            this.btnReset.UseVisualStyleBackColor = true;
            // 
            // btnAckAll
            // 
            this.btnAckAll.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnAckAll.Location = new System.Drawing.Point(722, 237);
            this.btnAckAll.Name = "btnAckAll";
            this.btnAckAll.Size = new System.Drawing.Size(75, 23);
            this.btnAckAll.TabIndex = 3;
            this.btnAckAll.Text = "Ack All";
            this.btnAckAll.UseVisualStyleBackColor = true;
            // 
            // tmrFlash
            // 
            this.tmrFlash.Interval = 1000;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(13, 18);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(32, 13);
            this.label1.TabIndex = 5;
            this.label1.Text = "Filter:";
            // 
            // cbxFilter
            // 
            this.cbxFilter.FormattingEnabled = true;
            this.cbxFilter.Items.AddRange(new object[] {
            "All",
            "Today",
            "Yesterday",
            "This week",
            "This month"});
            this.cbxFilter.Location = new System.Drawing.Point(54, 14);
            this.cbxFilter.Name = "cbxFilter";
            this.cbxFilter.Size = new System.Drawing.Size(121, 21);
            this.cbxFilter.TabIndex = 4;
            // 
            // AlarmViewer
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.BackColor = System.Drawing.SystemColors.ActiveCaption;
            this.Controls.Add(this.label1);
            this.Controls.Add(this.cbxFilter);
            this.Controls.Add(this.btnAckAll);
            this.Controls.Add(this.btnReset);
            this.Controls.Add(this.btnAck);
            this.Controls.Add(this.lstvAlarmItem);
            this.Name = "AlarmViewer";
            this.Size = new System.Drawing.Size(891, 273);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ListView lstvAlarmItem;
        private System.Windows.Forms.ColumnHeader colNo;
        private System.Windows.Forms.ColumnHeader colOccurrenceTime;
        private System.Windows.Forms.ColumnHeader colRestoreTime;
        private System.Windows.Forms.ColumnHeader colTagNo;
        private System.Windows.Forms.ColumnHeader colDescription;
        private System.Windows.Forms.ColumnHeader colStatus;
        private System.Windows.Forms.Button btnAck;
        private System.Windows.Forms.Button btnReset;
        private System.Windows.Forms.Button btnAckAll;
        private System.Windows.Forms.Timer tmrFlash;
        private System.Windows.Forms.ColumnHeader colLocation;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox cbxFilter;
        private System.Windows.Forms.ColumnHeader colFaultCode;
    }
}
