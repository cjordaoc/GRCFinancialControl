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
            this.groupConnection = new System.Windows.Forms.GroupBox();
            this.chkUseSsl = new System.Windows.Forms.CheckBox();
            this.numPort = new System.Windows.Forms.NumericUpDown();
            this.lblPort = new System.Windows.Forms.Label();
            this.txtPassword = new System.Windows.Forms.TextBox();
            this.lblPassword = new System.Windows.Forms.Label();
            this.txtUsername = new System.Windows.Forms.TextBox();
            this.lblUsername = new System.Windows.Forms.Label();
            this.txtDatabase = new System.Windows.Forms.TextBox();
            this.lblDatabase = new System.Windows.Forms.Label();
            this.txtServer = new System.Windows.Forms.TextBox();
            this.lblServer = new System.Windows.Forms.Label();
            this.groupEngagement = new System.Windows.Forms.GroupBox();
            this.chkDryRun = new System.Windows.Forms.CheckBox();
            this.dtpWeekEnd = new System.Windows.Forms.DateTimePicker();
            this.lblWeekEnd = new System.Windows.Forms.Label();
            this.txtSnapshotLabel = new System.Windows.Forms.TextBox();
            this.lblSnapshotLabel = new System.Windows.Forms.Label();
            this.txtEngagementId = new System.Windows.Forms.TextBox();
            this.lblEngagementId = new System.Windows.Forms.Label();
            this.flowButtons = new System.Windows.Forms.FlowLayoutPanel();
            this.btnLoadPlan = new System.Windows.Forms.Button();
            this.btnLoadEtc = new System.Windows.Forms.Button();
            this.btnLoadErp = new System.Windows.Forms.Button();
            this.btnLoadRetain = new System.Windows.Forms.Button();
            this.btnLoadCharges = new System.Windows.Forms.Button();
            this.btnReconcile = new System.Windows.Forms.Button();
            this.btnExportAudit = new System.Windows.Forms.Button();
            this.txtStatus = new System.Windows.Forms.TextBox();
            this.groupConnection.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numPort)).BeginInit();
            this.groupEngagement.SuspendLayout();
            this.flowButtons.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupConnection
            // 
            this.groupConnection.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupConnection.Controls.Add(this.chkUseSsl);
            this.groupConnection.Controls.Add(this.numPort);
            this.groupConnection.Controls.Add(this.lblPort);
            this.groupConnection.Controls.Add(this.txtPassword);
            this.groupConnection.Controls.Add(this.lblPassword);
            this.groupConnection.Controls.Add(this.txtUsername);
            this.groupConnection.Controls.Add(this.lblUsername);
            this.groupConnection.Controls.Add(this.txtDatabase);
            this.groupConnection.Controls.Add(this.lblDatabase);
            this.groupConnection.Controls.Add(this.txtServer);
            this.groupConnection.Controls.Add(this.lblServer);
            this.groupConnection.Location = new System.Drawing.Point(12, 12);
            this.groupConnection.Name = "groupConnection";
            this.groupConnection.Size = new System.Drawing.Size(466, 174);
            this.groupConnection.TabIndex = 0;
            this.groupConnection.TabStop = false;
            this.groupConnection.Text = "Database Connection";
            // 
            // chkUseSsl
            // 
            this.chkUseSsl.AutoSize = true;
            this.chkUseSsl.Location = new System.Drawing.Point(236, 139);
            this.chkUseSsl.Name = "chkUseSsl";
            this.chkUseSsl.Size = new System.Drawing.Size(66, 19);
            this.chkUseSsl.TabIndex = 6;
            this.chkUseSsl.Text = "Use SSL";
            this.chkUseSsl.UseVisualStyleBackColor = true;
            // 
            // numPort
            // 
            this.numPort.Location = new System.Drawing.Point(98, 138);
            this.numPort.Maximum = new decimal(new int[] {
            65535,
            0,
            0,
            0});
            this.numPort.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numPort.Name = "numPort";
            this.numPort.Size = new System.Drawing.Size(120, 23);
            this.numPort.TabIndex = 5;
            this.numPort.Value = new decimal(new int[] {
            3306,
            0,
            0,
            0});
            // 
            // lblPort
            // 
            this.lblPort.AutoSize = true;
            this.lblPort.Location = new System.Drawing.Point(16, 140);
            this.lblPort.Name = "lblPort";
            this.lblPort.Size = new System.Drawing.Size(30, 15);
            this.lblPort.TabIndex = 10;
            this.lblPort.Text = "Port";
            // 
            // txtPassword
            // 
            this.txtPassword.Location = new System.Drawing.Point(98, 109);
            this.txtPassword.Name = "txtPassword";
            this.txtPassword.PasswordChar = '●';
            this.txtPassword.Size = new System.Drawing.Size(340, 23);
            this.txtPassword.TabIndex = 4;
            // 
            // lblPassword
            // 
            this.lblPassword.AutoSize = true;
            this.lblPassword.Location = new System.Drawing.Point(16, 112);
            this.lblPassword.Name = "lblPassword";
            this.lblPassword.Size = new System.Drawing.Size(57, 15);
            this.lblPassword.TabIndex = 8;
            this.lblPassword.Text = "Password";
            // 
            // txtUsername
            // 
            this.txtUsername.Location = new System.Drawing.Point(98, 80);
            this.txtUsername.Name = "txtUsername";
            this.txtUsername.Size = new System.Drawing.Size(340, 23);
            this.txtUsername.TabIndex = 3;
            // 
            // lblUsername
            // 
            this.lblUsername.AutoSize = true;
            this.lblUsername.Location = new System.Drawing.Point(16, 83);
            this.lblUsername.Name = "lblUsername";
            this.lblUsername.Size = new System.Drawing.Size(60, 15);
            this.lblUsername.TabIndex = 6;
            this.lblUsername.Text = "Username";
            // 
            // txtDatabase
            // 
            this.txtDatabase.Location = new System.Drawing.Point(98, 51);
            this.txtDatabase.Name = "txtDatabase";
            this.txtDatabase.Size = new System.Drawing.Size(340, 23);
            this.txtDatabase.TabIndex = 2;
            // 
            // lblDatabase
            // 
            this.lblDatabase.AutoSize = true;
            this.lblDatabase.Location = new System.Drawing.Point(16, 54);
            this.lblDatabase.Name = "lblDatabase";
            this.lblDatabase.Size = new System.Drawing.Size(57, 15);
            this.lblDatabase.TabIndex = 4;
            this.lblDatabase.Text = "Database";
            // 
            // txtServer
            // 
            this.txtServer.Location = new System.Drawing.Point(98, 22);
            this.txtServer.Name = "txtServer";
            this.txtServer.Size = new System.Drawing.Size(340, 23);
            this.txtServer.TabIndex = 1;
            // 
            // lblServer
            // 
            this.lblServer.AutoSize = true;
            this.lblServer.Location = new System.Drawing.Point(16, 25);
            this.lblServer.Name = "lblServer";
            this.lblServer.Size = new System.Drawing.Size(39, 15);
            this.lblServer.TabIndex = 2;
            this.lblServer.Text = "Server";
            // 
            // groupEngagement
            // 
            this.groupEngagement.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupEngagement.Controls.Add(this.chkDryRun);
            this.groupEngagement.Controls.Add(this.dtpWeekEnd);
            this.groupEngagement.Controls.Add(this.lblWeekEnd);
            this.groupEngagement.Controls.Add(this.txtSnapshotLabel);
            this.groupEngagement.Controls.Add(this.lblSnapshotLabel);
            this.groupEngagement.Controls.Add(this.txtEngagementId);
            this.groupEngagement.Controls.Add(this.lblEngagementId);
            this.groupEngagement.Location = new System.Drawing.Point(484, 12);
            this.groupEngagement.Name = "groupEngagement";
            this.groupEngagement.Size = new System.Drawing.Size(408, 174);
            this.groupEngagement.TabIndex = 1;
            this.groupEngagement.TabStop = false;
            this.groupEngagement.Text = "Engagement Context";
            // 
            // chkDryRun
            // 
            this.chkDryRun.AutoSize = true;
            this.chkDryRun.Location = new System.Drawing.Point(20, 140);
            this.chkDryRun.Name = "chkDryRun";
            this.chkDryRun.Size = new System.Drawing.Size(187, 19);
            this.chkDryRun.TabIndex = 10;
            this.chkDryRun.Text = "Dry Run (validate without save)";
            this.chkDryRun.UseVisualStyleBackColor = true;
            // 
            // dtpWeekEnd
            // 
            this.dtpWeekEnd.CustomFormat = "yyyy-MM-dd";
            this.dtpWeekEnd.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            this.dtpWeekEnd.Location = new System.Drawing.Point(136, 109);
            this.dtpWeekEnd.Name = "dtpWeekEnd";
            this.dtpWeekEnd.Size = new System.Drawing.Size(200, 23);
            this.dtpWeekEnd.TabIndex = 9;
            // 
            // lblWeekEnd
            // 
            this.lblWeekEnd.AutoSize = true;
            this.lblWeekEnd.Location = new System.Drawing.Point(17, 112);
            this.lblWeekEnd.Name = "lblWeekEnd";
            this.lblWeekEnd.Size = new System.Drawing.Size(101, 15);
            this.lblWeekEnd.TabIndex = 8;
            this.lblWeekEnd.Text = "Week End (Reconcile)";
            // 
            // txtSnapshotLabel
            // 
            this.txtSnapshotLabel.Location = new System.Drawing.Point(136, 67);
            this.txtSnapshotLabel.Name = "txtSnapshotLabel";
            this.txtSnapshotLabel.Size = new System.Drawing.Size(240, 23);
            this.txtSnapshotLabel.TabIndex = 8;
            // 
            // lblSnapshotLabel
            // 
            this.lblSnapshotLabel.AutoSize = true;
            this.lblSnapshotLabel.Location = new System.Drawing.Point(17, 70);
            this.lblSnapshotLabel.Name = "lblSnapshotLabel";
            this.lblSnapshotLabel.Size = new System.Drawing.Size(88, 15);
            this.lblSnapshotLabel.TabIndex = 6;
            this.lblSnapshotLabel.Text = "Snapshot Label";
            // 
            // txtEngagementId
            // 
            this.txtEngagementId.Location = new System.Drawing.Point(136, 27);
            this.txtEngagementId.Name = "txtEngagementId";
            this.txtEngagementId.Size = new System.Drawing.Size(240, 23);
            this.txtEngagementId.TabIndex = 7;
            // 
            // lblEngagementId
            // 
            this.lblEngagementId.AutoSize = true;
            this.lblEngagementId.Location = new System.Drawing.Point(17, 30);
            this.lblEngagementId.Name = "lblEngagementId";
            this.lblEngagementId.Size = new System.Drawing.Size(88, 15);
            this.lblEngagementId.TabIndex = 4;
            this.lblEngagementId.Text = "Engagement ID";
            // 
            // flowButtons
            // 
            this.flowButtons.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.flowButtons.AutoSize = false;
            this.flowButtons.Controls.Add(this.btnLoadPlan);
            this.flowButtons.Controls.Add(this.btnLoadEtc);
            this.flowButtons.Controls.Add(this.btnLoadErp);
            this.flowButtons.Controls.Add(this.btnLoadRetain);
            this.flowButtons.Controls.Add(this.btnLoadCharges);
            this.flowButtons.Controls.Add(this.btnReconcile);
            this.flowButtons.Controls.Add(this.btnExportAudit);
            this.flowButtons.Location = new System.Drawing.Point(12, 198);
            this.flowButtons.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
            this.flowButtons.WrapContents = true;
            this.flowButtons.Name = "flowButtons";
            this.flowButtons.Size = new System.Drawing.Size(880, 75);
            this.flowButtons.TabIndex = 2;
            // 
            // btnLoadPlan
            // 
            this.btnLoadPlan.Location = new System.Drawing.Point(3, 3);
            this.btnLoadPlan.Name = "btnLoadPlan";
            this.btnLoadPlan.Size = new System.Drawing.Size(120, 32);
            this.btnLoadPlan.TabIndex = 11;
            this.btnLoadPlan.Text = "Load Plan";
            this.btnLoadPlan.UseVisualStyleBackColor = true;
            this.btnLoadPlan.Click += new System.EventHandler(this.btnLoadPlan_Click);
            // 
            // btnLoadEtc
            // 
            this.btnLoadEtc.Location = new System.Drawing.Point(129, 3);
            this.btnLoadEtc.Name = "btnLoadEtc";
            this.btnLoadEtc.Size = new System.Drawing.Size(120, 32);
            this.btnLoadEtc.TabIndex = 12;
            this.btnLoadEtc.Text = "Load ETC";
            this.btnLoadEtc.UseVisualStyleBackColor = true;
            this.btnLoadEtc.Click += new System.EventHandler(this.btnLoadEtc_Click);
            // 
            // btnLoadErp
            // 
            this.btnLoadErp.Location = new System.Drawing.Point(255, 3);
            this.btnLoadErp.Name = "btnLoadErp";
            this.btnLoadErp.Size = new System.Drawing.Size(120, 32);
            this.btnLoadErp.TabIndex = 13;
            this.btnLoadErp.Text = "Load ERP";
            this.btnLoadErp.UseVisualStyleBackColor = true;
            this.btnLoadErp.Click += new System.EventHandler(this.btnLoadErp_Click);
            // 
            // btnLoadRetain
            // 
            this.btnLoadRetain.Location = new System.Drawing.Point(381, 3);
            this.btnLoadRetain.Name = "btnLoadRetain";
            this.btnLoadRetain.Size = new System.Drawing.Size(120, 32);
            this.btnLoadRetain.TabIndex = 14;
            this.btnLoadRetain.Text = "Load Retain";
            this.btnLoadRetain.UseVisualStyleBackColor = true;
            this.btnLoadRetain.Click += new System.EventHandler(this.btnLoadRetain_Click);
            // 
            // btnLoadCharges
            // 
            this.btnLoadCharges.Location = new System.Drawing.Point(507, 3);
            this.btnLoadCharges.Name = "btnLoadCharges";
            this.btnLoadCharges.Size = new System.Drawing.Size(120, 32);
            this.btnLoadCharges.TabIndex = 15;
            this.btnLoadCharges.Text = "Load Charges";
            this.btnLoadCharges.UseVisualStyleBackColor = true;
            this.btnLoadCharges.Click += new System.EventHandler(this.btnLoadCharges_Click);
            // 
            // btnReconcile
            // 
            this.btnReconcile.Location = new System.Drawing.Point(633, 3);
            this.btnReconcile.Name = "btnReconcile";
            this.btnReconcile.Size = new System.Drawing.Size(120, 32);
            this.btnReconcile.TabIndex = 16;
            this.btnReconcile.Text = "Reconcile ETC";
            this.btnReconcile.UseVisualStyleBackColor = true;
            this.btnReconcile.Click += new System.EventHandler(this.btnReconcile_Click);
            // 
            // btnExportAudit
            // 
            this.btnExportAudit.Location = new System.Drawing.Point(759, 3);
            this.btnExportAudit.Name = "btnExportAudit";
            this.btnExportAudit.Size = new System.Drawing.Size(120, 32);
            this.btnExportAudit.TabIndex = 17;
            this.btnExportAudit.Text = "Export Audit";
            this.btnExportAudit.UseVisualStyleBackColor = true;
            this.btnExportAudit.Click += new System.EventHandler(this.btnExportAudit_Click);
            // 
            // txtStatus
            // 
            this.txtStatus.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtStatus.Location = new System.Drawing.Point(12, 279);
            this.txtStatus.Multiline = true;
            this.txtStatus.Name = "txtStatus";
            this.txtStatus.ReadOnly = true;
            this.txtStatus.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtStatus.Size = new System.Drawing.Size(880, 310);
            this.txtStatus.TabIndex = 3;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(904, 601);
            this.Controls.Add(this.txtStatus);
            this.Controls.Add(this.flowButtons);
            this.Controls.Add(this.groupEngagement);
            this.Controls.Add(this.groupConnection);
            this.MinimumSize = new System.Drawing.Size(920, 640);
            this.Name = "Form1";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "GRC Financial Control Loader";
            this.groupConnection.ResumeLayout(false);
            this.groupConnection.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numPort)).EndInit();
            this.groupEngagement.ResumeLayout(false);
            this.groupEngagement.PerformLayout();
            this.flowButtons.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.GroupBox groupConnection;
        private System.Windows.Forms.CheckBox chkUseSsl;
        private System.Windows.Forms.NumericUpDown numPort;
        private System.Windows.Forms.Label lblPort;
        private System.Windows.Forms.TextBox txtPassword;
        private System.Windows.Forms.Label lblPassword;
        private System.Windows.Forms.TextBox txtUsername;
        private System.Windows.Forms.Label lblUsername;
        private System.Windows.Forms.TextBox txtDatabase;
        private System.Windows.Forms.Label lblDatabase;
        private System.Windows.Forms.TextBox txtServer;
        private System.Windows.Forms.Label lblServer;
        private System.Windows.Forms.GroupBox groupEngagement;
        private System.Windows.Forms.CheckBox chkDryRun;
        private System.Windows.Forms.DateTimePicker dtpWeekEnd;
        private System.Windows.Forms.Label lblWeekEnd;
        private System.Windows.Forms.TextBox txtSnapshotLabel;
        private System.Windows.Forms.Label lblSnapshotLabel;
        private System.Windows.Forms.TextBox txtEngagementId;
        private System.Windows.Forms.Label lblEngagementId;
        private System.Windows.Forms.FlowLayoutPanel flowButtons;
        private System.Windows.Forms.Button btnLoadPlan;
        private System.Windows.Forms.Button btnLoadEtc;
        private System.Windows.Forms.Button btnLoadErp;
        private System.Windows.Forms.Button btnLoadRetain;
        private System.Windows.Forms.Button btnLoadCharges;
        private System.Windows.Forms.Button btnReconcile;
        private System.Windows.Forms.Button btnExportAudit;
        private System.Windows.Forms.TextBox txtStatus;
    }
}
