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
            menuMain = new MenuStrip();
            fileToolStripMenuItem = new ToolStripMenuItem();
            databaseConnectionsToolStripMenuItem = new ToolStripMenuItem();
            maintenanceToolStripMenuItem = new ToolStripMenuItem();
            selectDefaultToolStripMenuItem = new ToolStripMenuItem();
            measurementPeriodToolStripMenuItem = new ToolStripMenuItem();
            fileSeparator = new ToolStripSeparator();
            exitToolStripMenuItem = new ToolStripMenuItem();
            uploadsToolStripMenuItem = new ToolStripMenuItem();
            uploadPlanToolStripMenuItem = new ToolStripMenuItem();
            uploadEtcToolStripMenuItem = new ToolStripMenuItem();
            uploadMarginDataToolStripMenuItem = new ToolStripMenuItem();
            uploadErpToolStripMenuItem = new ToolStripMenuItem();
            uploadRetainToolStripMenuItem = new ToolStripMenuItem();
            uploadChargesToolStripMenuItem = new ToolStripMenuItem();
            masterDataToolStripMenuItem = new ToolStripMenuItem();
            engagementsToolStripMenuItem = new ToolStripMenuItem();
            fiscalYearsToolStripMenuItem = new ToolStripMenuItem();
            reportsToolStripMenuItem = new ToolStripMenuItem();
            reconcileToolStripMenuItem = new ToolStripMenuItem();
            exportAuditToolStripMenuItem = new ToolStripMenuItem();
            helpToolStripMenuItem = new ToolStripMenuItem();
            viewHelpToolStripMenuItem = new ToolStripMenuItem();
            layoutMain = new TableLayoutPanel();
            flowWeekEnd = new FlowLayoutPanel();
            lblWeekEnd = new Label();
            dtpWeekEnd = new DateTimePicker();
            gridUploadSummary = new DataGridView();
            txtStatus = new TextBox();
            menuMain.SuspendLayout();
            layoutMain.SuspendLayout();
            flowWeekEnd.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)gridUploadSummary).BeginInit();
            SuspendLayout();
            //
            // menuMain
            //
            menuMain.ImageScalingSize = new Size(24, 24);
            menuMain.Items.AddRange(new ToolStripItem[] { fileToolStripMenuItem, uploadsToolStripMenuItem, masterDataToolStripMenuItem, reportsToolStripMenuItem, helpToolStripMenuItem });
            menuMain.Location = new Point(0, 0);
            menuMain.Name = "menuMain";
            menuMain.Padding = new Padding(9, 3, 0, 3);
            menuMain.Size = new Size(1002, 35);
            menuMain.TabIndex = 0;
            menuMain.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            fileToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { databaseConnectionsToolStripMenuItem, measurementPeriodToolStripMenuItem, fileSeparator, exitToolStripMenuItem });
            fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            fileToolStripMenuItem.Size = new Size(54, 29);
            fileToolStripMenuItem.Text = "File";
            // 
            // databaseConnectionsToolStripMenuItem
            // 
            databaseConnectionsToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { maintenanceToolStripMenuItem, selectDefaultToolStripMenuItem });
            databaseConnectionsToolStripMenuItem.Name = "databaseConnectionsToolStripMenuItem";
            databaseConnectionsToolStripMenuItem.Size = new Size(291, 34);
            databaseConnectionsToolStripMenuItem.Text = "Database Connections";
            // 
            // maintenanceToolStripMenuItem
            // 
            maintenanceToolStripMenuItem.Name = "maintenanceToolStripMenuItem";
            maintenanceToolStripMenuItem.Size = new Size(234, 34);
            maintenanceToolStripMenuItem.Text = "Maintenance...";
            maintenanceToolStripMenuItem.Click += maintenanceToolStripMenuItem_Click;
            // 
            // selectDefaultToolStripMenuItem
            // 
            selectDefaultToolStripMenuItem.Name = "selectDefaultToolStripMenuItem";
            selectDefaultToolStripMenuItem.Size = new Size(234, 34);
            selectDefaultToolStripMenuItem.Text = "Select Default...";
            selectDefaultToolStripMenuItem.Click += selectDefaultToolStripMenuItem_Click;
            // 
            // measurementPeriodToolStripMenuItem
            // 
            measurementPeriodToolStripMenuItem.Name = "measurementPeriodToolStripMenuItem";
            measurementPeriodToolStripMenuItem.Size = new Size(291, 34);
            measurementPeriodToolStripMenuItem.Text = "Measurement Period...";
            measurementPeriodToolStripMenuItem.Click += measurementPeriodToolStripMenuItem_Click;
            // 
            // fileSeparator
            // 
            fileSeparator.Name = "fileSeparator";
            fileSeparator.Size = new Size(288, 6);
            // 
            // exitToolStripMenuItem
            // 
            exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            exitToolStripMenuItem.Size = new Size(291, 34);
            exitToolStripMenuItem.Text = "Exit";
            exitToolStripMenuItem.Click += exitToolStripMenuItem_Click;
            // 
            // uploadsToolStripMenuItem
            // 
            uploadsToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { uploadPlanToolStripMenuItem, uploadEtcToolStripMenuItem, uploadMarginDataToolStripMenuItem, uploadErpToolStripMenuItem, uploadRetainToolStripMenuItem, uploadChargesToolStripMenuItem });
            uploadsToolStripMenuItem.Name = "uploadsToolStripMenuItem";
            uploadsToolStripMenuItem.Size = new Size(94, 29);
            uploadsToolStripMenuItem.Text = "Uploads";
            // 
            // uploadPlanToolStripMenuItem
            // 
            uploadPlanToolStripMenuItem.Name = "uploadPlanToolStripMenuItem";
            uploadPlanToolStripMenuItem.Size = new Size(267, 34);
            uploadPlanToolStripMenuItem.Text = "Load Plan";
            uploadPlanToolStripMenuItem.Click += uploadPlanToolStripMenuItem_Click;
            // 
            // uploadEtcToolStripMenuItem
            // 
            uploadEtcToolStripMenuItem.Name = "uploadEtcToolStripMenuItem";
            uploadEtcToolStripMenuItem.Size = new Size(267, 34);
            uploadEtcToolStripMenuItem.Text = "Load ETC";
            uploadEtcToolStripMenuItem.Click += uploadEtcToolStripMenuItem_Click;
            // 
            // uploadMarginDataToolStripMenuItem
            // 
            uploadMarginDataToolStripMenuItem.Name = "uploadMarginDataToolStripMenuItem";
            uploadMarginDataToolStripMenuItem.Size = new Size(267, 34);
            uploadMarginDataToolStripMenuItem.Text = "Load Margin Data";
            uploadMarginDataToolStripMenuItem.Click += uploadMarginDataToolStripMenuItem_Click;
            // 
            // uploadErpToolStripMenuItem
            // 
            uploadErpToolStripMenuItem.Name = "uploadErpToolStripMenuItem";
            uploadErpToolStripMenuItem.Size = new Size(267, 34);
            uploadErpToolStripMenuItem.Text = "Load ERP Weekly";
            uploadErpToolStripMenuItem.Click += uploadErpToolStripMenuItem_Click;
            // 
            // uploadRetainToolStripMenuItem
            // 
            uploadRetainToolStripMenuItem.Name = "uploadRetainToolStripMenuItem";
            uploadRetainToolStripMenuItem.Size = new Size(267, 34);
            uploadRetainToolStripMenuItem.Text = "Load Retain Weekly";
            uploadRetainToolStripMenuItem.Click += uploadRetainToolStripMenuItem_Click;
            // 
            // uploadChargesToolStripMenuItem
            // 
            uploadChargesToolStripMenuItem.Name = "uploadChargesToolStripMenuItem";
            uploadChargesToolStripMenuItem.Size = new Size(267, 34);
            uploadChargesToolStripMenuItem.Text = "Load Charges";
            uploadChargesToolStripMenuItem.Click += uploadChargesToolStripMenuItem_Click;
            // 
            // masterDataToolStripMenuItem
            // 
            masterDataToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { engagementsToolStripMenuItem, fiscalYearsToolStripMenuItem });
            masterDataToolStripMenuItem.Name = "masterDataToolStripMenuItem";
            masterDataToolStripMenuItem.Size = new Size(124, 29);
            masterDataToolStripMenuItem.Text = "Master Data";
            // 
            // engagementsToolStripMenuItem
            // 
            engagementsToolStripMenuItem.Name = "engagementsToolStripMenuItem";
            engagementsToolStripMenuItem.Size = new Size(222, 34);
            engagementsToolStripMenuItem.Text = "Engagements";
            engagementsToolStripMenuItem.Click += engagementsToolStripMenuItem_Click;
            // 
            // fiscalYearsToolStripMenuItem
            // 
            fiscalYearsToolStripMenuItem.Name = "fiscalYearsToolStripMenuItem";
            fiscalYearsToolStripMenuItem.Size = new Size(222, 34);
            fiscalYearsToolStripMenuItem.Text = "Fiscal Years";
            fiscalYearsToolStripMenuItem.Click += fiscalYearsToolStripMenuItem_Click;
            // 
            // reportsToolStripMenuItem
            // 
            reportsToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { reconcileToolStripMenuItem, exportAuditToolStripMenuItem });
            reportsToolStripMenuItem.Name = "reportsToolStripMenuItem";
            reportsToolStripMenuItem.Size = new Size(89, 29);
            reportsToolStripMenuItem.Text = "Reports";
            // 
            // reconcileToolStripMenuItem
            // 
            reconcileToolStripMenuItem.Name = "reconcileToolStripMenuItem";
            reconcileToolStripMenuItem.Size = new Size(220, 34);
            reconcileToolStripMenuItem.Text = "Reconcile ETC";
            reconcileToolStripMenuItem.Click += reconcileToolStripMenuItem_Click;
            // 
            // exportAuditToolStripMenuItem
            // 
            exportAuditToolStripMenuItem.Name = "exportAuditToolStripMenuItem";
            exportAuditToolStripMenuItem.Size = new Size(220, 34);
            exportAuditToolStripMenuItem.Text = "Export Audit";
            exportAuditToolStripMenuItem.Click += exportAuditToolStripMenuItem_Click;
            // 
            // helpToolStripMenuItem
            // 
            helpToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { viewHelpToolStripMenuItem });
            helpToolStripMenuItem.Name = "helpToolStripMenuItem";
            helpToolStripMenuItem.Size = new Size(65, 29);
            helpToolStripMenuItem.Text = "Help";
            // 
            // viewHelpToolStripMenuItem
            // 
            viewHelpToolStripMenuItem.Name = "viewHelpToolStripMenuItem";
            viewHelpToolStripMenuItem.Size = new Size(193, 34);
            viewHelpToolStripMenuItem.Text = "View Help";
            viewHelpToolStripMenuItem.Click += viewHelpToolStripMenuItem_Click;
            //
            // layoutMain
            //
            layoutMain.ColumnCount = 1;
            layoutMain.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layoutMain.Controls.Add(flowWeekEnd, 0, 0);
            layoutMain.Controls.Add(gridUploadSummary, 0, 1);
            layoutMain.Controls.Add(txtStatus, 0, 2);
            layoutMain.Dock = DockStyle.Fill;
            layoutMain.Location = new Point(0, 35);
            layoutMain.Name = "layoutMain";
            layoutMain.RowCount = 3;
            layoutMain.RowStyles.Add(new RowStyle());
            layoutMain.RowStyles.Add(new RowStyle(SizeType.Percent, 55F));
            layoutMain.RowStyles.Add(new RowStyle(SizeType.Percent, 45F));
            layoutMain.Size = new Size(1002, 677);
            layoutMain.TabIndex = 1;
            //
            // flowWeekEnd
            //
            flowWeekEnd.AutoSize = true;
            flowWeekEnd.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            flowWeekEnd.Controls.Add(lblWeekEnd);
            flowWeekEnd.Controls.Add(dtpWeekEnd);
            flowWeekEnd.Dock = DockStyle.Fill;
            flowWeekEnd.Location = new Point(3, 3);
            flowWeekEnd.Name = "flowWeekEnd";
            flowWeekEnd.Size = new Size(996, 38);
            flowWeekEnd.TabIndex = 0;
            flowWeekEnd.WrapContents = false;
            //
            // lblWeekEnd
            //
            lblWeekEnd.Anchor = AnchorStyles.Left;
            lblWeekEnd.AutoSize = true;
            lblWeekEnd.Location = new Point(3, 7);
            lblWeekEnd.Name = "lblWeekEnd";
            lblWeekEnd.Padding = new Padding(0, 6, 6, 0);
            lblWeekEnd.Size = new Size(126, 31);
            lblWeekEnd.TabIndex = 0;
            lblWeekEnd.Text = "Week ending:";
            lblWeekEnd.TextAlign = ContentAlignment.MiddleLeft;
            //
            // dtpWeekEnd
            //
            dtpWeekEnd.Format = DateTimePickerFormat.Short;
            dtpWeekEnd.Location = new Point(135, 3);
            dtpWeekEnd.Margin = new Padding(3, 3, 15, 3);
            dtpWeekEnd.Name = "dtpWeekEnd";
            dtpWeekEnd.Size = new Size(150, 31);
            dtpWeekEnd.TabIndex = 1;
            //
            // gridUploadSummary
            //
            gridUploadSummary.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            gridUploadSummary.Dock = DockStyle.Fill;
            gridUploadSummary.Location = new Point(3, 47);
            gridUploadSummary.Margin = new Padding(3, 3, 3, 6);
            gridUploadSummary.Name = "gridUploadSummary";
            gridUploadSummary.RowHeadersWidth = 62;
            gridUploadSummary.RowTemplate.Height = 33;
            gridUploadSummary.Size = new Size(996, 363);
            gridUploadSummary.TabIndex = 2;
            //
            // txtStatus
            //
            txtStatus.Dock = DockStyle.Fill;
            txtStatus.Location = new Point(3, 419);
            txtStatus.Margin = new Padding(3);
            txtStatus.Multiline = true;
            txtStatus.Name = "txtStatus";
            txtStatus.ReadOnly = true;
            txtStatus.ScrollBars = ScrollBars.Vertical;
            txtStatus.Size = new Size(996, 255);
            txtStatus.TabIndex = 3;
            //
            // Form1
            //
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1002, 712);
            Controls.Add(layoutMain);
            Controls.Add(menuMain);
            MainMenuStrip = menuMain;
            Margin = new Padding(4, 5, 4, 5);
            MinimumSize = new Size(1024, 768);
            Name = "Form1";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "GRC Financial Control Loader";
            flowWeekEnd.ResumeLayout(false);
            flowWeekEnd.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)gridUploadSummary).EndInit();
            layoutMain.ResumeLayout(false);
            layoutMain.PerformLayout();
            menuMain.ResumeLayout(false);
            menuMain.PerformLayout();
            ResumeLayout(false);
            PerformLayout();

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
        private System.Windows.Forms.ToolStripMenuItem measurementPeriodToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem engagementsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem fiscalYearsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem reportsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem reconcileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exportAuditToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem helpToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem viewHelpToolStripMenuItem;
        private System.Windows.Forms.TableLayoutPanel layoutMain;
        private System.Windows.Forms.FlowLayoutPanel flowWeekEnd;
        private System.Windows.Forms.Label lblWeekEnd;
        private System.Windows.Forms.DateTimePicker dtpWeekEnd;
        private System.Windows.Forms.DataGridView gridUploadSummary;
        private System.Windows.Forms.TextBox txtStatus;
    }
}
