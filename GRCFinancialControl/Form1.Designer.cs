namespace GRCFinancialControl
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.menuMain = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.databaseConnectionsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.maintenanceToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.selectDefaultToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.fileSeparator = new System.Windows.Forms.ToolStripSeparator();
            this.exitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.uploadsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.uploadPlanToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.uploadEtcToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.uploadMarginDataToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.uploadErpToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.uploadRetainToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.uploadChargesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.masterDataToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.measurementPeriodsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.fiscalYearsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.reportsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.reconcileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exportAuditToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.helpToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.viewHelpToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.groupEngagement = new System.Windows.Forms.GroupBox();
            this.dtpWeekEnd = new System.Windows.Forms.DateTimePicker();
            this.lblWeekEnd = new System.Windows.Forms.Label();
            this.txtEngagementId = new System.Windows.Forms.TextBox();
            this.lblEngagementId = new System.Windows.Forms.Label();
            this.txtStatus = new System.Windows.Forms.TextBox();
            this.gridUploadSummary = new System.Windows.Forms.DataGridView();
            this.menuMain.SuspendLayout();
            this.groupEngagement.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridUploadSummary)).BeginInit();
            this.SuspendLayout();
            // 
            // menuMain
            // 
            this.menuMain.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.uploadsToolStripMenuItem,
            this.masterDataToolStripMenuItem,
            this.reportsToolStripMenuItem,
            this.helpToolStripMenuItem});
            this.menuMain.Location = new System.Drawing.Point(0, 0);
            this.menuMain.Name = "menuMain";
            this.menuMain.Size = new System.Drawing.Size(904, 24);
            this.menuMain.TabIndex = 0;
            this.menuMain.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.databaseConnectionsToolStripMenuItem,
            this.fileSeparator,
            this.exitToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
            this.fileToolStripMenuItem.Text = "File";
            // 
            // databaseConnectionsToolStripMenuItem
            // 
            this.databaseConnectionsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.maintenanceToolStripMenuItem,
            this.selectDefaultToolStripMenuItem});
            this.databaseConnectionsToolStripMenuItem.Name = "databaseConnectionsToolStripMenuItem";
            this.databaseConnectionsToolStripMenuItem.Size = new System.Drawing.Size(189, 22);
            this.databaseConnectionsToolStripMenuItem.Text = "Database Connections";
            // 
            // maintenanceToolStripMenuItem
            // 
            this.maintenanceToolStripMenuItem.Name = "maintenanceToolStripMenuItem";
            this.maintenanceToolStripMenuItem.Size = new System.Drawing.Size(156, 22);
            this.maintenanceToolStripMenuItem.Text = "Maintenance...";
            this.maintenanceToolStripMenuItem.Click += new System.EventHandler(this.maintenanceToolStripMenuItem_Click);
            // 
            // selectDefaultToolStripMenuItem
            // 
            this.selectDefaultToolStripMenuItem.Name = "selectDefaultToolStripMenuItem";
            this.selectDefaultToolStripMenuItem.Size = new System.Drawing.Size(156, 22);
            this.selectDefaultToolStripMenuItem.Text = "Select Default...";
            this.selectDefaultToolStripMenuItem.Click += new System.EventHandler(this.selectDefaultToolStripMenuItem_Click);
            // 
            // fileSeparator
            // 
            this.fileSeparator.Name = "fileSeparator";
            this.fileSeparator.Size = new System.Drawing.Size(186, 6);
            // 
            // exitToolStripMenuItem
            // 
            this.exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            this.exitToolStripMenuItem.Size = new System.Drawing.Size(189, 22);
            this.exitToolStripMenuItem.Text = "Exit";
            this.exitToolStripMenuItem.Click += new System.EventHandler(this.exitToolStripMenuItem_Click);
            // 
            // uploadsToolStripMenuItem
            // 
            this.uploadsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.uploadPlanToolStripMenuItem,
            this.uploadEtcToolStripMenuItem,
            this.uploadMarginDataToolStripMenuItem,
            this.uploadErpToolStripMenuItem,
            this.uploadRetainToolStripMenuItem,
            this.uploadChargesToolStripMenuItem});
            this.uploadsToolStripMenuItem.Name = "uploadsToolStripMenuItem";
            this.uploadsToolStripMenuItem.Size = new System.Drawing.Size(62, 20);
            this.uploadsToolStripMenuItem.Text = "Uploads";
            // 
            // uploadPlanToolStripMenuItem
            // 
            this.uploadPlanToolStripMenuItem.Name = "uploadPlanToolStripMenuItem";
            this.uploadPlanToolStripMenuItem.Size = new System.Drawing.Size(184, 22);
            this.uploadPlanToolStripMenuItem.Text = "Load Plan";
            this.uploadPlanToolStripMenuItem.Click += new System.EventHandler(this.uploadPlanToolStripMenuItem_Click);
            // 
            // uploadEtcToolStripMenuItem
            //
            this.uploadEtcToolStripMenuItem.Name = "uploadEtcToolStripMenuItem";
            this.uploadEtcToolStripMenuItem.Size = new System.Drawing.Size(184, 22);
            this.uploadEtcToolStripMenuItem.Text = "Load ETC";
            this.uploadEtcToolStripMenuItem.Click += new System.EventHandler(this.uploadEtcToolStripMenuItem_Click);
            //
            // uploadMarginDataToolStripMenuItem
            //
            this.uploadMarginDataToolStripMenuItem.Name = "uploadMarginDataToolStripMenuItem";
            this.uploadMarginDataToolStripMenuItem.Size = new System.Drawing.Size(184, 22);
            this.uploadMarginDataToolStripMenuItem.Text = "Load Margin Data";
            this.uploadMarginDataToolStripMenuItem.Click += new System.EventHandler(this.uploadMarginDataToolStripMenuItem_Click);
            // 
            // uploadErpToolStripMenuItem
            // 
            this.uploadErpToolStripMenuItem.Name = "uploadErpToolStripMenuItem";
            this.uploadErpToolStripMenuItem.Size = new System.Drawing.Size(184, 22);
            this.uploadErpToolStripMenuItem.Text = "Load ERP Weekly";
            this.uploadErpToolStripMenuItem.Click += new System.EventHandler(this.uploadErpToolStripMenuItem_Click);
            // 
            // uploadRetainToolStripMenuItem
            // 
            this.uploadRetainToolStripMenuItem.Name = "uploadRetainToolStripMenuItem";
            this.uploadRetainToolStripMenuItem.Size = new System.Drawing.Size(184, 22);
            this.uploadRetainToolStripMenuItem.Text = "Load Retain Weekly";
            this.uploadRetainToolStripMenuItem.Click += new System.EventHandler(this.uploadRetainToolStripMenuItem_Click);
            // 
            // uploadChargesToolStripMenuItem
            //
            this.uploadChargesToolStripMenuItem.Name = "uploadChargesToolStripMenuItem";
            this.uploadChargesToolStripMenuItem.Size = new System.Drawing.Size(184, 22);
            this.uploadChargesToolStripMenuItem.Text = "Load Charges";
            this.uploadChargesToolStripMenuItem.Click += new System.EventHandler(this.uploadChargesToolStripMenuItem_Click);
            //
            // masterDataToolStripMenuItem
            //
            this.masterDataToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.measurementPeriodsToolStripMenuItem,
            this.fiscalYearsToolStripMenuItem});
            this.masterDataToolStripMenuItem.Name = "masterDataToolStripMenuItem";
            this.masterDataToolStripMenuItem.Size = new System.Drawing.Size(86, 20);
            this.masterDataToolStripMenuItem.Text = "Master Data";
            //
            // measurementPeriodsToolStripMenuItem
            //
            this.measurementPeriodsToolStripMenuItem.Name = "measurementPeriodsToolStripMenuItem";
            this.measurementPeriodsToolStripMenuItem.Size = new System.Drawing.Size(197, 22);
            this.measurementPeriodsToolStripMenuItem.Text = "Measurement Periods";
            this.measurementPeriodsToolStripMenuItem.Click += new System.EventHandler(this.measurementPeriodsToolStripMenuItem_Click);
            //
            // fiscalYearsToolStripMenuItem
            //
            this.fiscalYearsToolStripMenuItem.Name = "fiscalYearsToolStripMenuItem";
            this.fiscalYearsToolStripMenuItem.Size = new System.Drawing.Size(197, 22);
            this.fiscalYearsToolStripMenuItem.Text = "Fiscal Years";
            this.fiscalYearsToolStripMenuItem.Click += new System.EventHandler(this.fiscalYearsToolStripMenuItem_Click);
            //
            // reportsToolStripMenuItem
            //
            this.reportsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.reconcileToolStripMenuItem,
            this.exportAuditToolStripMenuItem});
            this.reportsToolStripMenuItem.Name = "reportsToolStripMenuItem";
            this.reportsToolStripMenuItem.Size = new System.Drawing.Size(59, 20);
            this.reportsToolStripMenuItem.Text = "Reports";
            // 
            // reconcileToolStripMenuItem
            // 
            this.reconcileToolStripMenuItem.Name = "reconcileToolStripMenuItem";
            this.reconcileToolStripMenuItem.Size = new System.Drawing.Size(149, 22);
            this.reconcileToolStripMenuItem.Text = "Reconcile ETC";
            this.reconcileToolStripMenuItem.Click += new System.EventHandler(this.reconcileToolStripMenuItem_Click);
            // 
            // exportAuditToolStripMenuItem
            // 
            this.exportAuditToolStripMenuItem.Name = "exportAuditToolStripMenuItem";
            this.exportAuditToolStripMenuItem.Size = new System.Drawing.Size(149, 22);
            this.exportAuditToolStripMenuItem.Text = "Export Audit";
            this.exportAuditToolStripMenuItem.Click += new System.EventHandler(this.exportAuditToolStripMenuItem_Click);
            // 
            // helpToolStripMenuItem
            // 
            this.helpToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.viewHelpToolStripMenuItem});
            this.helpToolStripMenuItem.Name = "helpToolStripMenuItem";
            this.helpToolStripMenuItem.Size = new System.Drawing.Size(44, 20);
            this.helpToolStripMenuItem.Text = "Help";
            // 
            // viewHelpToolStripMenuItem
            // 
            this.viewHelpToolStripMenuItem.Name = "viewHelpToolStripMenuItem";
            this.viewHelpToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.viewHelpToolStripMenuItem.Text = "View Help";
            this.viewHelpToolStripMenuItem.Click += new System.EventHandler(this.viewHelpToolStripMenuItem_Click);
            // 
            // groupEngagement
            // 
            this.groupEngagement.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupEngagement.Controls.Add(this.dtpWeekEnd);
            this.groupEngagement.Controls.Add(this.lblWeekEnd);
            this.groupEngagement.Controls.Add(this.txtEngagementId);
            this.groupEngagement.Controls.Add(this.lblEngagementId);
            this.groupEngagement.Location = new System.Drawing.Point(12, 36);
            this.groupEngagement.Name = "groupEngagement";
            this.groupEngagement.Size = new System.Drawing.Size(880, 120);
            this.groupEngagement.TabIndex = 1;
            this.groupEngagement.TabStop = false;
            this.groupEngagement.Text = "Engagement Context";
            // 
            // dtpWeekEnd
            // 
            this.dtpWeekEnd.CustomFormat = "yyyy-MM-dd";
            this.dtpWeekEnd.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            this.dtpWeekEnd.Location = new System.Drawing.Point(138, 67);
            this.dtpWeekEnd.Name = "dtpWeekEnd";
            this.dtpWeekEnd.Size = new System.Drawing.Size(200, 23);
            this.dtpWeekEnd.TabIndex = 3;
            //
            // lblWeekEnd
            //
            this.lblWeekEnd.AutoSize = true;
            this.lblWeekEnd.Location = new System.Drawing.Point(19, 70);
            this.lblWeekEnd.Name = "lblWeekEnd";
            this.lblWeekEnd.Size = new System.Drawing.Size(101, 15);
            this.lblWeekEnd.TabIndex = 2;
            this.lblWeekEnd.Text = "Week End (Reconcile)";
            //
            // txtEngagementId
            //
            this.txtEngagementId.Location = new System.Drawing.Point(138, 27);
            this.txtEngagementId.Name = "txtEngagementId";
            this.txtEngagementId.Size = new System.Drawing.Size(320, 23);
            this.txtEngagementId.TabIndex = 1;
            // 
            // lblEngagementId
            // 
            this.lblEngagementId.AutoSize = true;
            this.lblEngagementId.Location = new System.Drawing.Point(19, 30);
            this.lblEngagementId.Name = "lblEngagementId";
            this.lblEngagementId.Size = new System.Drawing.Size(88, 15);
            this.lblEngagementId.TabIndex = 0;
            this.lblEngagementId.Text = "Engagement ID";
            //
            // gridUploadSummary
            //
            this.gridUploadSummary.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.gridUploadSummary.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridUploadSummary.Location = new System.Drawing.Point(12, 162);
            this.gridUploadSummary.Name = "gridUploadSummary";
            this.gridUploadSummary.ReadOnly = true;
            this.gridUploadSummary.Size = new System.Drawing.Size(880, 180);
            this.gridUploadSummary.TabIndex = 2;
            //
            // txtStatus
            //
            this.txtStatus.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtStatus.Location = new System.Drawing.Point(12, 348);
            this.txtStatus.Multiline = true;
            this.txtStatus.Name = "txtStatus";
            this.txtStatus.ReadOnly = true;
            this.txtStatus.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtStatus.Size = new System.Drawing.Size(880, 241);
            this.txtStatus.TabIndex = 3;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(904, 601);
            this.Controls.Add(this.txtStatus);
            this.Controls.Add(this.gridUploadSummary);
            this.Controls.Add(this.groupEngagement);
            this.Controls.Add(this.menuMain);
            this.MainMenuStrip = this.menuMain;
            this.MinimumSize = new System.Drawing.Size(920, 640);
            this.Name = "Form1";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "GRC Financial Control Loader";
            this.menuMain.ResumeLayout(false);
            this.menuMain.PerformLayout();
            this.groupEngagement.ResumeLayout(false);
            this.groupEngagement.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridUploadSummary)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.MenuStrip menuMain;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem databaseConnectionsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem maintenanceToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem selectDefaultToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator fileSeparator;
        private System.Windows.Forms.ToolStripMenuItem exitToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem uploadsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem uploadPlanToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem uploadEtcToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem uploadMarginDataToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem uploadErpToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem uploadRetainToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem uploadChargesToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem masterDataToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem measurementPeriodsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem fiscalYearsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem reportsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem reconcileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exportAuditToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem helpToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem viewHelpToolStripMenuItem;
        private System.Windows.Forms.GroupBox groupEngagement;
        private System.Windows.Forms.DateTimePicker dtpWeekEnd;
        private System.Windows.Forms.Label lblWeekEnd;
        private System.Windows.Forms.TextBox txtEngagementId;
        private System.Windows.Forms.Label lblEngagementId;
        private System.Windows.Forms.TextBox txtStatus;
        private System.Windows.Forms.DataGridView gridUploadSummary;
    }
}
