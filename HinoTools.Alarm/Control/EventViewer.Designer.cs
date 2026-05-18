
namespace HinoTools.Alarm.Control
{
    partial class EventViewer
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
            this.lstvEventItem = new System.Windows.Forms.ListView();
            this.colNo = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colOccurrenceTime = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colTagNo = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colLocation = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colDescription = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colStatus = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.cbxFilter = new System.Windows.Forms.ComboBox();
            this.label1 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // lstvEventItem
            // 
            this.lstvEventItem.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lstvEventItem.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colNo,
            this.colOccurrenceTime,
            this.colTagNo,
            this.colLocation,
            this.colDescription,
            this.colStatus});
            this.lstvEventItem.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lstvEventItem.FullRowSelect = true;
            this.lstvEventItem.GridLines = true;
            this.lstvEventItem.HideSelection = false;
            this.lstvEventItem.Location = new System.Drawing.Point(14, 44);
            this.lstvEventItem.MultiSelect = false;
            this.lstvEventItem.Name = "lstvEventItem";
            this.lstvEventItem.Size = new System.Drawing.Size(632, 184);
            this.lstvEventItem.TabIndex = 1;
            this.lstvEventItem.UseCompatibleStateImageBehavior = false;
            this.lstvEventItem.View = System.Windows.Forms.View.Details;
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
            // colTagNo
            // 
            this.colTagNo.Text = "Tag No.";
            this.colTagNo.Width = 130;
            // 
            // colLocation
            // 
            this.colLocation.Text = "Location";
            this.colLocation.Width = 120;
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
            // cbxFilter
            // 
            this.cbxFilter.FormattingEnabled = true;
            this.cbxFilter.Items.AddRange(new object[] {
            "All",
            "Today",
            "Yesterday",
            "This week",
            "This month"});
            this.cbxFilter.Location = new System.Drawing.Point(55, 12);
            this.cbxFilter.Name = "cbxFilter";
            this.cbxFilter.Size = new System.Drawing.Size(121, 21);
            this.cbxFilter.TabIndex = 2;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(14, 16);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(32, 13);
            this.label1.TabIndex = 3;
            this.label1.Text = "Filter:";
            // 
            // EventViewer
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.BackColor = System.Drawing.SystemColors.ActiveCaption;
            this.Controls.Add(this.label1);
            this.Controls.Add(this.cbxFilter);
            this.Controls.Add(this.lstvEventItem);
            this.Name = "EventViewer";
            this.Size = new System.Drawing.Size(660, 244);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ListView lstvEventItem;
        private System.Windows.Forms.ColumnHeader colNo;
        private System.Windows.Forms.ColumnHeader colOccurrenceTime;
        private System.Windows.Forms.ColumnHeader colTagNo;
        private System.Windows.Forms.ColumnHeader colDescription;
        private System.Windows.Forms.ColumnHeader colStatus;
        private System.Windows.Forms.ColumnHeader colLocation;
        private System.Windows.Forms.ComboBox cbxFilter;
        private System.Windows.Forms.Label label1;
    }
}
