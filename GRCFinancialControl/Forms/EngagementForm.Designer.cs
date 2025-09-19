namespace GRCFinancialControl.Forms
{
    partial class EngagementForm
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
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            this.tableLayoutPanelEditors = new System.Windows.Forms.TableLayoutPanel();
            this.lblEngagementId = new System.Windows.Forms.Label();
            this.txtEngagementId = new System.Windows.Forms.TextBox();
            this.lblEngagementTitle = new System.Windows.Forms.Label();
            this.txtEngagementTitle = new System.Windows.Forms.TextBox();
            this.lblEngagementPartner = new System.Windows.Forms.Label();
            this.txtEngagementPartner = new System.Windows.Forms.TextBox();
            this.lblEngagementManager = new System.Windows.Forms.Label();
            this.txtEngagementManager = new System.Windows.Forms.TextBox();
            this.lblOpeningMargin = new System.Windows.Forms.Label();
            this.txtOpeningMargin = new System.Windows.Forms.TextBox();
            this.gridEngagements = new System.Windows.Forms.DataGridView();
            this.colEngagementId = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colEngagementTitle = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colEngagementPartner = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colEngagementManager = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colOpeningMargin = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colIsActive = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.colCreatedUtc = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colUpdatedUtc = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.flowLayoutPanelButtons = new System.Windows.Forms.FlowLayoutPanel();
            this.btnNew = new System.Windows.Forms.Button();
            this.btnSave = new System.Windows.Forms.Button();
            this.btnDelete = new System.Windows.Forms.Button();
            this.btnRefresh = new System.Windows.Forms.Button();
            this.tableLayoutPanelEditors.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridEngagements)).BeginInit();
            this.flowLayoutPanelButtons.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanelEditors
            // 
            this.tableLayoutPanelEditors.AutoSize = true;
            this.tableLayoutPanelEditors.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tableLayoutPanelEditors.ColumnCount = 2;
            this.tableLayoutPanelEditors.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanelEditors.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelEditors.Controls.Add(this.lblEngagementId, 0, 0);
            this.tableLayoutPanelEditors.Controls.Add(this.txtEngagementId, 1, 0);
            this.tableLayoutPanelEditors.Controls.Add(this.lblEngagementTitle, 0, 1);
            this.tableLayoutPanelEditors.Controls.Add(this.txtEngagementTitle, 1, 1);
            this.tableLayoutPanelEditors.Controls.Add(this.lblEngagementPartner, 0, 2);
            this.tableLayoutPanelEditors.Controls.Add(this.txtEngagementPartner, 1, 2);
            this.tableLayoutPanelEditors.Controls.Add(this.lblEngagementManager, 0, 3);
            this.tableLayoutPanelEditors.Controls.Add(this.txtEngagementManager, 1, 3);
            this.tableLayoutPanelEditors.Controls.Add(this.lblOpeningMargin, 0, 4);
            this.tableLayoutPanelEditors.Controls.Add(this.txtOpeningMargin, 1, 4);
            this.tableLayoutPanelEditors.Dock = System.Windows.Forms.DockStyle.Top;
            this.tableLayoutPanelEditors.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanelEditors.Margin = new System.Windows.Forms.Padding(6);
            this.tableLayoutPanelEditors.Name = "tableLayoutPanelEditors";
            this.tableLayoutPanelEditors.Padding = new System.Windows.Forms.Padding(10, 10, 10, 0);
            this.tableLayoutPanelEditors.RowCount = 5;
            this.tableLayoutPanelEditors.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanelEditors.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanelEditors.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanelEditors.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanelEditors.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanelEditors.Size = new System.Drawing.Size(984, 155);
            this.tableLayoutPanelEditors.TabIndex = 0;
            // 
            // lblEngagementId
            // 
            this.lblEngagementId.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblEngagementId.AutoSize = true;
            this.lblEngagementId.Location = new System.Drawing.Point(13, 15);
            this.lblEngagementId.Margin = new System.Windows.Forms.Padding(3, 5, 3, 5);
            this.lblEngagementId.Name = "lblEngagementId";
            this.lblEngagementId.Size = new System.Drawing.Size(91, 15);
            this.lblEngagementId.TabIndex = 0;
            this.lblEngagementId.Text = "Engagement ID";
            // 
            // txtEngagementId
            // 
            this.txtEngagementId.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtEngagementId.Location = new System.Drawing.Point(142, 13);
            this.txtEngagementId.Margin = new System.Windows.Forms.Padding(3, 3, 10, 3);
            this.txtEngagementId.MaxLength = 64;
            this.txtEngagementId.Name = "txtEngagementId";
            this.txtEngagementId.Size = new System.Drawing.Size(829, 23);
            this.txtEngagementId.TabIndex = 1;
            this.txtEngagementId.TextChanged += new System.EventHandler(this.Editor_TextChanged);
            // 
            // lblEngagementTitle
            // 
            this.lblEngagementTitle.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblEngagementTitle.AutoSize = true;
            this.lblEngagementTitle.Location = new System.Drawing.Point(13, 48);
            this.lblEngagementTitle.Margin = new System.Windows.Forms.Padding(3, 5, 3, 5);
            this.lblEngagementTitle.Name = "lblEngagementTitle";
            this.lblEngagementTitle.Size = new System.Drawing.Size(104, 15);
            this.lblEngagementTitle.TabIndex = 2;
            this.lblEngagementTitle.Text = "Engagement Name";
            // 
            // txtEngagementTitle
            // 
            this.txtEngagementTitle.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtEngagementTitle.Location = new System.Drawing.Point(142, 45);
            this.txtEngagementTitle.Margin = new System.Windows.Forms.Padding(3, 3, 10, 3);
            this.txtEngagementTitle.MaxLength = 255;
            this.txtEngagementTitle.Name = "txtEngagementTitle";
            this.txtEngagementTitle.Size = new System.Drawing.Size(829, 23);
            this.txtEngagementTitle.TabIndex = 3;
            this.txtEngagementTitle.TextChanged += new System.EventHandler(this.Editor_TextChanged);
            // 
            // lblEngagementPartner
            // 
            this.lblEngagementPartner.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblEngagementPartner.AutoSize = true;
            this.lblEngagementPartner.Location = new System.Drawing.Point(13, 81);
            this.lblEngagementPartner.Margin = new System.Windows.Forms.Padding(3, 5, 3, 5);
            this.lblEngagementPartner.Name = "lblEngagementPartner";
            this.lblEngagementPartner.Size = new System.Drawing.Size(109, 15);
            this.lblEngagementPartner.TabIndex = 4;
            this.lblEngagementPartner.Text = "Engagement Partner";
            // 
            // txtEngagementPartner
            // 
            this.txtEngagementPartner.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtEngagementPartner.Location = new System.Drawing.Point(142, 78);
            this.txtEngagementPartner.Margin = new System.Windows.Forms.Padding(3, 3, 10, 3);
            this.txtEngagementPartner.MaxLength = 255;
            this.txtEngagementPartner.Name = "txtEngagementPartner";
            this.txtEngagementPartner.Size = new System.Drawing.Size(829, 23);
            this.txtEngagementPartner.TabIndex = 5;
            this.txtEngagementPartner.TextChanged += new System.EventHandler(this.Editor_TextChanged);
            // 
            // lblEngagementManager
            // 
            this.lblEngagementManager.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblEngagementManager.AutoSize = true;
            this.lblEngagementManager.Location = new System.Drawing.Point(13, 114);
            this.lblEngagementManager.Margin = new System.Windows.Forms.Padding(3, 5, 3, 5);
            this.lblEngagementManager.Name = "lblEngagementManager";
            this.lblEngagementManager.Size = new System.Drawing.Size(115, 15);
            this.lblEngagementManager.TabIndex = 6;
            this.lblEngagementManager.Text = "Engagement Manager";
            // 
            // txtEngagementManager
            // 
            this.txtEngagementManager.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtEngagementManager.Location = new System.Drawing.Point(142, 111);
            this.txtEngagementManager.Margin = new System.Windows.Forms.Padding(3, 3, 10, 3);
            this.txtEngagementManager.MaxLength = 255;
            this.txtEngagementManager.Name = "txtEngagementManager";
            this.txtEngagementManager.Size = new System.Drawing.Size(829, 23);
            this.txtEngagementManager.TabIndex = 7;
            this.txtEngagementManager.TextChanged += new System.EventHandler(this.Editor_TextChanged);
            // 
            // lblOpeningMargin
            // 
            this.lblOpeningMargin.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblOpeningMargin.AutoSize = true;
            this.lblOpeningMargin.Location = new System.Drawing.Point(13, 144);
            this.lblOpeningMargin.Margin = new System.Windows.Forms.Padding(3, 5, 3, 5);
            this.lblOpeningMargin.Name = "lblOpeningMargin";
            this.lblOpeningMargin.Size = new System.Drawing.Size(97, 15);
            this.lblOpeningMargin.TabIndex = 8;
            this.lblOpeningMargin.Text = "Opening Margin %";
            // 
            // txtOpeningMargin
            // 
            this.txtOpeningMargin.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.txtOpeningMargin.Location = new System.Drawing.Point(142, 141);
            this.txtOpeningMargin.Margin = new System.Windows.Forms.Padding(3, 3, 10, 3);
            this.txtOpeningMargin.MaxLength = 16;
            this.txtOpeningMargin.Name = "txtOpeningMargin";
            this.txtOpeningMargin.Size = new System.Drawing.Size(120, 23);
            this.txtOpeningMargin.TabIndex = 9;
            this.txtOpeningMargin.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.txtOpeningMargin.TextChanged += new System.EventHandler(this.Editor_TextChanged);
            // 
            // gridEngagements
            // 
            this.gridEngagements.AllowUserToAddRows = false;
            this.gridEngagements.AllowUserToDeleteRows = false;
            this.gridEngagements.AllowUserToResizeRows = false;
            this.gridEngagements.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.gridEngagements.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridEngagements.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colEngagementId,
            this.colEngagementTitle,
            this.colEngagementPartner,
            this.colEngagementManager,
            this.colOpeningMargin,
            this.colIsActive,
            this.colCreatedUtc,
            this.colUpdatedUtc});
            this.gridEngagements.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridEngagements.Location = new System.Drawing.Point(0, 155);
            this.gridEngagements.MultiSelect = false;
            this.gridEngagements.Name = "gridEngagements";
            this.gridEngagements.ReadOnly = true;
            this.gridEngagements.RowHeadersVisible = false;
            this.gridEngagements.RowTemplate.Height = 25;
            this.gridEngagements.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.gridEngagements.Size = new System.Drawing.Size(984, 356);
            this.gridEngagements.TabIndex = 1;
            this.gridEngagements.SelectionChanged += new System.EventHandler(this.gridEngagements_SelectionChanged);
            // 
            // colEngagementId
            // 
            this.colEngagementId.DataPropertyName = "EngagementId";
            this.colEngagementId.HeaderText = "ID";
            this.colEngagementId.MinimumWidth = 100;
            this.colEngagementId.Name = "colEngagementId";
            this.colEngagementId.ReadOnly = true;
            // 
            // colEngagementTitle
            // 
            this.colEngagementTitle.DataPropertyName = "EngagementTitle";
            this.colEngagementTitle.HeaderText = "Name";
            this.colEngagementTitle.MinimumWidth = 150;
            this.colEngagementTitle.Name = "colEngagementTitle";
            this.colEngagementTitle.ReadOnly = true;
            // 
            // colEngagementPartner
            // 
            this.colEngagementPartner.DataPropertyName = "EngagementPartner";
            this.colEngagementPartner.HeaderText = "Partner";
            this.colEngagementPartner.MinimumWidth = 120;
            this.colEngagementPartner.Name = "colEngagementPartner";
            this.colEngagementPartner.ReadOnly = true;
            // 
            // colEngagementManager
            // 
            this.colEngagementManager.DataPropertyName = "EngagementManager";
            this.colEngagementManager.HeaderText = "Manager";
            this.colEngagementManager.MinimumWidth = 120;
            this.colEngagementManager.Name = "colEngagementManager";
            this.colEngagementManager.ReadOnly = true;
            // 
            // colOpeningMargin
            // 
            this.colOpeningMargin.DataPropertyName = "OpeningMargin";
            this.colOpeningMargin.HeaderText = "Opening Margin";
            this.colOpeningMargin.MinimumWidth = 110;
            this.colOpeningMargin.Name = "colOpeningMargin";
            this.colOpeningMargin.ReadOnly = true;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            dataGridViewCellStyle1.Format = "N3";
            dataGridViewCellStyle1.NullValue = null;
            this.colOpeningMargin.DefaultCellStyle = dataGridViewCellStyle1;
            // 
            // colIsActive
            // 
            this.colIsActive.DataPropertyName = "IsActive";
            this.colIsActive.HeaderText = "Active";
            this.colIsActive.MinimumWidth = 60;
            this.colIsActive.Name = "colIsActive";
            this.colIsActive.ReadOnly = true;
            // 
            // colCreatedUtc
            // 
            this.colCreatedUtc.DataPropertyName = "CreatedUtc";
            this.colCreatedUtc.HeaderText = "Created (UTC)";
            this.colCreatedUtc.MinimumWidth = 120;
            this.colCreatedUtc.Name = "colCreatedUtc";
            this.colCreatedUtc.ReadOnly = true;
            dataGridViewCellStyle2.Format = "g";
            dataGridViewCellStyle2.NullValue = null;
            this.colCreatedUtc.DefaultCellStyle = dataGridViewCellStyle2;
            // 
            // colUpdatedUtc
            // 
            this.colUpdatedUtc.DataPropertyName = "UpdatedUtc";
            this.colUpdatedUtc.HeaderText = "Updated (UTC)";
            this.colUpdatedUtc.MinimumWidth = 120;
            this.colUpdatedUtc.Name = "colUpdatedUtc";
            this.colUpdatedUtc.ReadOnly = true;
            dataGridViewCellStyle3.Format = "g";
            dataGridViewCellStyle3.NullValue = null;
            this.colUpdatedUtc.DefaultCellStyle = dataGridViewCellStyle3;
            // 
            // flowLayoutPanelButtons
            // 
            this.flowLayoutPanelButtons.AutoSize = true;
            this.flowLayoutPanelButtons.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.flowLayoutPanelButtons.Controls.Add(this.btnNew);
            this.flowLayoutPanelButtons.Controls.Add(this.btnSave);
            this.flowLayoutPanelButtons.Controls.Add(this.btnDelete);
            this.flowLayoutPanelButtons.Controls.Add(this.btnRefresh);
            this.flowLayoutPanelButtons.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.flowLayoutPanelButtons.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
            this.flowLayoutPanelButtons.Location = new System.Drawing.Point(0, 511);
            this.flowLayoutPanelButtons.Margin = new System.Windows.Forms.Padding(10);
            this.flowLayoutPanelButtons.Name = "flowLayoutPanelButtons";
            this.flowLayoutPanelButtons.Padding = new System.Windows.Forms.Padding(10);
            this.flowLayoutPanelButtons.Size = new System.Drawing.Size(984, 60);
            this.flowLayoutPanelButtons.TabIndex = 2;
            this.flowLayoutPanelButtons.WrapContents = false;
            // 
            // btnNew
            // 
            this.btnNew.AutoSize = true;
            this.btnNew.Location = new System.Drawing.Point(13, 13);
            this.btnNew.Margin = new System.Windows.Forms.Padding(3);
            this.btnNew.Name = "btnNew";
            this.btnNew.Size = new System.Drawing.Size(90, 30);
            this.btnNew.TabIndex = 0;
            this.btnNew.Text = "New";
            this.btnNew.UseVisualStyleBackColor = true;
            this.btnNew.Click += new System.EventHandler(this.btnNew_Click);
            // 
            // btnSave
            // 
            this.btnSave.AutoSize = true;
            this.btnSave.Enabled = false;
            this.btnSave.Location = new System.Drawing.Point(109, 13);
            this.btnSave.Margin = new System.Windows.Forms.Padding(3);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(90, 30);
            this.btnSave.TabIndex = 1;
            this.btnSave.Text = "Save";
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            // 
            // btnDelete
            // 
            this.btnDelete.AutoSize = true;
            this.btnDelete.Enabled = false;
            this.btnDelete.Location = new System.Drawing.Point(205, 13);
            this.btnDelete.Margin = new System.Windows.Forms.Padding(3);
            this.btnDelete.Name = "btnDelete";
            this.btnDelete.Size = new System.Drawing.Size(90, 30);
            this.btnDelete.TabIndex = 2;
            this.btnDelete.Text = "Delete";
            this.btnDelete.UseVisualStyleBackColor = true;
            this.btnDelete.Click += new System.EventHandler(this.btnDelete_Click);
            // 
            // btnRefresh
            // 
            this.btnRefresh.AutoSize = true;
            this.btnRefresh.Location = new System.Drawing.Point(301, 13);
            this.btnRefresh.Margin = new System.Windows.Forms.Padding(3);
            this.btnRefresh.Name = "btnRefresh";
            this.btnRefresh.Size = new System.Drawing.Size(90, 30);
            this.btnRefresh.TabIndex = 3;
            this.btnRefresh.Text = "Refresh";
            this.btnRefresh.UseVisualStyleBackColor = true;
            this.btnRefresh.Click += new System.EventHandler(this.btnRefresh_Click);
            // 
            // EngagementForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(984, 571);
            this.Controls.Add(this.tableLayoutPanelEditors);
            this.Controls.Add(this.flowLayoutPanelButtons);
            this.Controls.Add(this.gridEngagements);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(800, 500);
            this.Name = "EngagementForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Engagements";
            this.tableLayoutPanelEditors.ResumeLayout(false);
            this.tableLayoutPanelEditors.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridEngagements)).EndInit();
            this.flowLayoutPanelButtons.ResumeLayout(false);
            this.flowLayoutPanelButtons.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelEditors;
        private System.Windows.Forms.Label lblEngagementId;
        private System.Windows.Forms.TextBox txtEngagementId;
        private System.Windows.Forms.Label lblEngagementTitle;
        private System.Windows.Forms.TextBox txtEngagementTitle;
        private System.Windows.Forms.Label lblEngagementPartner;
        private System.Windows.Forms.TextBox txtEngagementPartner;
        private System.Windows.Forms.Label lblEngagementManager;
        private System.Windows.Forms.TextBox txtEngagementManager;
        private System.Windows.Forms.Label lblOpeningMargin;
        private System.Windows.Forms.TextBox txtOpeningMargin;
        private System.Windows.Forms.DataGridView gridEngagements;
        private System.Windows.Forms.DataGridViewTextBoxColumn colEngagementId;
        private System.Windows.Forms.DataGridViewTextBoxColumn colEngagementTitle;
        private System.Windows.Forms.DataGridViewTextBoxColumn colEngagementPartner;
        private System.Windows.Forms.DataGridViewTextBoxColumn colEngagementManager;
        private System.Windows.Forms.DataGridViewTextBoxColumn colOpeningMargin;
        private System.Windows.Forms.DataGridViewCheckBoxColumn colIsActive;
        private System.Windows.Forms.DataGridViewTextBoxColumn colCreatedUtc;
        private System.Windows.Forms.DataGridViewTextBoxColumn colUpdatedUtc;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanelButtons;
        private System.Windows.Forms.Button btnNew;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.Button btnDelete;
        private System.Windows.Forms.Button btnRefresh;
    }
}
