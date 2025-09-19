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
            DataGridViewCellStyle dataGridViewCellStyle1 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle2 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle3 = new DataGridViewCellStyle();
            tableLayoutPanelEditors = new TableLayoutPanel();
            lblEngagementId = new Label();
            txtEngagementId = new TextBox();
            lblEngagementTitle = new Label();
            txtEngagementTitle = new TextBox();
            lblEngagementPartner = new Label();
            txtEngagementPartner = new TextBox();
            lblEngagementManager = new Label();
            txtEngagementManager = new TextBox();
            lblOpeningMargin = new Label();
            txtOpeningMargin = new TextBox();
            gridEngagements = new DataGridView();
            colEngagementId = new DataGridViewTextBoxColumn();
            colEngagementTitle = new DataGridViewTextBoxColumn();
            colEngagementPartner = new DataGridViewTextBoxColumn();
            colEngagementManager = new DataGridViewTextBoxColumn();
            colOpeningMargin = new DataGridViewTextBoxColumn();
            colIsActive = new DataGridViewCheckBoxColumn();
            colCreatedUtc = new DataGridViewTextBoxColumn();
            colUpdatedUtc = new DataGridViewTextBoxColumn();
            flowLayoutPanelButtons = new FlowLayoutPanel();
            btnNew = new Button();
            btnSave = new Button();
            btnDelete = new Button();
            btnRefresh = new Button();
            tableLayoutPanelEditors.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)gridEngagements).BeginInit();
            flowLayoutPanelButtons.SuspendLayout();
            SuspendLayout();
            // 
            // tableLayoutPanelEditors
            // 
            tableLayoutPanelEditors.AutoSize = true;
            tableLayoutPanelEditors.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            tableLayoutPanelEditors.ColumnCount = 2;
            tableLayoutPanelEditors.ColumnStyles.Add(new ColumnStyle());
            tableLayoutPanelEditors.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tableLayoutPanelEditors.Controls.Add(lblEngagementId, 0, 0);
            tableLayoutPanelEditors.Controls.Add(txtEngagementId, 1, 0);
            tableLayoutPanelEditors.Controls.Add(lblEngagementTitle, 0, 1);
            tableLayoutPanelEditors.Controls.Add(txtEngagementTitle, 1, 1);
            tableLayoutPanelEditors.Controls.Add(lblEngagementPartner, 0, 2);
            tableLayoutPanelEditors.Controls.Add(txtEngagementPartner, 1, 2);
            tableLayoutPanelEditors.Controls.Add(lblEngagementManager, 0, 3);
            tableLayoutPanelEditors.Controls.Add(txtEngagementManager, 1, 3);
            tableLayoutPanelEditors.Controls.Add(lblOpeningMargin, 0, 4);
            tableLayoutPanelEditors.Controls.Add(txtOpeningMargin, 1, 4);
            tableLayoutPanelEditors.Dock = DockStyle.Top;
            tableLayoutPanelEditors.Location = new Point(0, 0);
            tableLayoutPanelEditors.Margin = new Padding(9, 10, 9, 10);
            tableLayoutPanelEditors.Name = "tableLayoutPanelEditors";
            tableLayoutPanelEditors.Padding = new Padding(14, 17, 14, 0);
            tableLayoutPanelEditors.RowCount = 5;
            tableLayoutPanelEditors.RowStyles.Add(new RowStyle());
            tableLayoutPanelEditors.RowStyles.Add(new RowStyle());
            tableLayoutPanelEditors.RowStyles.Add(new RowStyle());
            tableLayoutPanelEditors.RowStyles.Add(new RowStyle());
            tableLayoutPanelEditors.RowStyles.Add(new RowStyle());
            tableLayoutPanelEditors.Size = new Size(1181, 222);
            tableLayoutPanelEditors.TabIndex = 0;
            // 
            // lblEngagementId
            // 
            lblEngagementId.Anchor = AnchorStyles.Left;
            lblEngagementId.AutoSize = true;
            lblEngagementId.Location = new Point(18, 25);
            lblEngagementId.Margin = new Padding(4, 8, 4, 8);
            lblEngagementId.Name = "lblEngagementId";
            lblEngagementId.Size = new Size(135, 25);
            lblEngagementId.TabIndex = 0;
            lblEngagementId.Text = "Engagement ID";
            // 
            // txtEngagementId
            // 
            txtEngagementId.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtEngagementId.Location = new Point(213, 22);
            txtEngagementId.Margin = new Padding(4, 5, 14, 5);
            txtEngagementId.MaxLength = 64;
            txtEngagementId.Name = "txtEngagementId";
            txtEngagementId.Size = new Size(940, 31);
            txtEngagementId.TabIndex = 1;
            txtEngagementId.TextChanged += Editor_TextChanged;
            // 
            // lblEngagementTitle
            // 
            lblEngagementTitle.Anchor = AnchorStyles.Left;
            lblEngagementTitle.AutoSize = true;
            lblEngagementTitle.Location = new Point(18, 66);
            lblEngagementTitle.Margin = new Padding(4, 8, 4, 8);
            lblEngagementTitle.Name = "lblEngagementTitle";
            lblEngagementTitle.Size = new Size(164, 25);
            lblEngagementTitle.TabIndex = 2;
            lblEngagementTitle.Text = "Engagement Name";
            // 
            // txtEngagementTitle
            // 
            txtEngagementTitle.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtEngagementTitle.Location = new Point(213, 63);
            txtEngagementTitle.Margin = new Padding(4, 5, 14, 5);
            txtEngagementTitle.MaxLength = 255;
            txtEngagementTitle.Name = "txtEngagementTitle";
            txtEngagementTitle.Size = new Size(940, 31);
            txtEngagementTitle.TabIndex = 3;
            txtEngagementTitle.TextChanged += Editor_TextChanged;
            // 
            // lblEngagementPartner
            // 
            lblEngagementPartner.Anchor = AnchorStyles.Left;
            lblEngagementPartner.AutoSize = true;
            lblEngagementPartner.Location = new Point(18, 107);
            lblEngagementPartner.Margin = new Padding(4, 8, 4, 8);
            lblEngagementPartner.Name = "lblEngagementPartner";
            lblEngagementPartner.Size = new Size(172, 25);
            lblEngagementPartner.TabIndex = 4;
            lblEngagementPartner.Text = "Engagement Partner";
            // 
            // txtEngagementPartner
            // 
            txtEngagementPartner.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtEngagementPartner.Location = new Point(213, 104);
            txtEngagementPartner.Margin = new Padding(4, 5, 14, 5);
            txtEngagementPartner.MaxLength = 255;
            txtEngagementPartner.Name = "txtEngagementPartner";
            txtEngagementPartner.Size = new Size(940, 31);
            txtEngagementPartner.TabIndex = 5;
            txtEngagementPartner.TextChanged += Editor_TextChanged;
            // 
            // lblEngagementManager
            // 
            lblEngagementManager.Anchor = AnchorStyles.Left;
            lblEngagementManager.AutoSize = true;
            lblEngagementManager.Location = new Point(18, 148);
            lblEngagementManager.Margin = new Padding(4, 8, 4, 8);
            lblEngagementManager.Name = "lblEngagementManager";
            lblEngagementManager.Size = new Size(187, 25);
            lblEngagementManager.TabIndex = 6;
            lblEngagementManager.Text = "Engagement Manager";
            // 
            // txtEngagementManager
            // 
            txtEngagementManager.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtEngagementManager.Location = new Point(213, 145);
            txtEngagementManager.Margin = new Padding(4, 5, 14, 5);
            txtEngagementManager.MaxLength = 255;
            txtEngagementManager.Name = "txtEngagementManager";
            txtEngagementManager.Size = new Size(940, 31);
            txtEngagementManager.TabIndex = 7;
            txtEngagementManager.TextChanged += Editor_TextChanged;
            // 
            // lblOpeningMargin
            // 
            lblOpeningMargin.Anchor = AnchorStyles.Left;
            lblOpeningMargin.AutoSize = true;
            lblOpeningMargin.Location = new Point(18, 189);
            lblOpeningMargin.Margin = new Padding(4, 8, 4, 8);
            lblOpeningMargin.Name = "lblOpeningMargin";
            lblOpeningMargin.Size = new Size(162, 25);
            lblOpeningMargin.TabIndex = 8;
            lblOpeningMargin.Text = "Opening Margin %";
            // 
            // txtOpeningMargin
            // 
            txtOpeningMargin.Anchor = AnchorStyles.Left;
            txtOpeningMargin.Location = new Point(213, 186);
            txtOpeningMargin.Margin = new Padding(4, 5, 14, 5);
            txtOpeningMargin.MaxLength = 16;
            txtOpeningMargin.Name = "txtOpeningMargin";
            txtOpeningMargin.Size = new Size(170, 31);
            txtOpeningMargin.TabIndex = 9;
            txtOpeningMargin.TextAlign = HorizontalAlignment.Right;
            txtOpeningMargin.TextChanged += Editor_TextChanged;
            // 
            // gridEngagements
            // 
            gridEngagements.AllowUserToAddRows = false;
            gridEngagements.AllowUserToDeleteRows = false;
            gridEngagements.AllowUserToResizeRows = false;
            gridEngagements.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            gridEngagements.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            gridEngagements.Columns.AddRange(new DataGridViewColumn[] { colEngagementId, colEngagementTitle, colEngagementPartner, colEngagementManager, colOpeningMargin, colIsActive, colCreatedUtc, colUpdatedUtc });
            gridEngagements.Dock = DockStyle.Bottom;
            gridEngagements.Location = new Point(0, 341);
            gridEngagements.Margin = new Padding(4, 5, 4, 5);
            gridEngagements.MultiSelect = false;
            gridEngagements.Name = "gridEngagements";
            gridEngagements.ReadOnly = true;
            gridEngagements.RowHeadersVisible = false;
            gridEngagements.RowHeadersWidth = 62;
            gridEngagements.RowTemplate.Height = 25;
            gridEngagements.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            gridEngagements.Size = new Size(1181, 507);
            gridEngagements.TabIndex = 1;
            gridEngagements.SelectionChanged += gridEngagements_SelectionChanged;
            // 
            // colEngagementId
            // 
            colEngagementId.DataPropertyName = "EngagementId";
            colEngagementId.HeaderText = "ID";
            colEngagementId.MinimumWidth = 100;
            colEngagementId.Name = "colEngagementId";
            colEngagementId.ReadOnly = true;
            // 
            // colEngagementTitle
            // 
            colEngagementTitle.DataPropertyName = "EngagementTitle";
            colEngagementTitle.HeaderText = "Name";
            colEngagementTitle.MinimumWidth = 150;
            colEngagementTitle.Name = "colEngagementTitle";
            colEngagementTitle.ReadOnly = true;
            // 
            // colEngagementPartner
            // 
            colEngagementPartner.DataPropertyName = "EngagementPartner";
            colEngagementPartner.HeaderText = "Partner";
            colEngagementPartner.MinimumWidth = 120;
            colEngagementPartner.Name = "colEngagementPartner";
            colEngagementPartner.ReadOnly = true;
            // 
            // colEngagementManager
            // 
            colEngagementManager.DataPropertyName = "EngagementManager";
            colEngagementManager.HeaderText = "Manager";
            colEngagementManager.MinimumWidth = 120;
            colEngagementManager.Name = "colEngagementManager";
            colEngagementManager.ReadOnly = true;
            // 
            // colOpeningMargin
            // 
            colOpeningMargin.DataPropertyName = "OpeningMargin";
            dataGridViewCellStyle1.Alignment = DataGridViewContentAlignment.MiddleRight;
            dataGridViewCellStyle1.Format = "N3";
            dataGridViewCellStyle1.NullValue = null;
            colOpeningMargin.DefaultCellStyle = dataGridViewCellStyle1;
            colOpeningMargin.HeaderText = "Opening Margin";
            colOpeningMargin.MinimumWidth = 110;
            colOpeningMargin.Name = "colOpeningMargin";
            colOpeningMargin.ReadOnly = true;
            // 
            // colIsActive
            // 
            colIsActive.DataPropertyName = "IsActive";
            colIsActive.HeaderText = "Active";
            colIsActive.MinimumWidth = 60;
            colIsActive.Name = "colIsActive";
            colIsActive.ReadOnly = true;
            // 
            // colCreatedUtc
            // 
            colCreatedUtc.DataPropertyName = "CreatedUtc";
            dataGridViewCellStyle2.Format = "g";
            dataGridViewCellStyle2.NullValue = null;
            colCreatedUtc.DefaultCellStyle = dataGridViewCellStyle2;
            colCreatedUtc.HeaderText = "Created (UTC)";
            colCreatedUtc.MinimumWidth = 120;
            colCreatedUtc.Name = "colCreatedUtc";
            colCreatedUtc.ReadOnly = true;
            // 
            // colUpdatedUtc
            // 
            colUpdatedUtc.DataPropertyName = "UpdatedUtc";
            dataGridViewCellStyle3.Format = "g";
            dataGridViewCellStyle3.NullValue = null;
            colUpdatedUtc.DefaultCellStyle = dataGridViewCellStyle3;
            colUpdatedUtc.HeaderText = "Updated (UTC)";
            colUpdatedUtc.MinimumWidth = 120;
            colUpdatedUtc.Name = "colUpdatedUtc";
            colUpdatedUtc.ReadOnly = true;
            // 
            // flowLayoutPanelButtons
            // 
            flowLayoutPanelButtons.AutoSize = true;
            flowLayoutPanelButtons.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            flowLayoutPanelButtons.Controls.Add(btnNew);
            flowLayoutPanelButtons.Controls.Add(btnSave);
            flowLayoutPanelButtons.Controls.Add(btnDelete);
            flowLayoutPanelButtons.Controls.Add(btnRefresh);
            flowLayoutPanelButtons.Dock = DockStyle.Bottom;
            flowLayoutPanelButtons.Location = new Point(0, 239);
            flowLayoutPanelButtons.Margin = new Padding(14, 17, 14, 17);
            flowLayoutPanelButtons.Name = "flowLayoutPanelButtons";
            flowLayoutPanelButtons.Padding = new Padding(14, 17, 14, 17);
            flowLayoutPanelButtons.Size = new Size(1181, 102);
            flowLayoutPanelButtons.TabIndex = 2;
            flowLayoutPanelButtons.WrapContents = false;
            // 
            // btnNew
            // 
            btnNew.AutoSize = true;
            btnNew.Location = new Point(18, 22);
            btnNew.Margin = new Padding(4, 5, 4, 5);
            btnNew.Name = "btnNew";
            btnNew.Size = new Size(129, 58);
            btnNew.TabIndex = 0;
            btnNew.Text = "New";
            btnNew.UseVisualStyleBackColor = true;
            btnNew.Click += btnNew_Click;
            // 
            // btnSave
            // 
            btnSave.AutoSize = true;
            btnSave.Enabled = false;
            btnSave.Location = new Point(155, 22);
            btnSave.Margin = new Padding(4, 5, 4, 5);
            btnSave.Name = "btnSave";
            btnSave.Size = new Size(129, 58);
            btnSave.TabIndex = 1;
            btnSave.Text = "Save";
            btnSave.UseVisualStyleBackColor = true;
            btnSave.Click += btnSave_Click;
            // 
            // btnDelete
            // 
            btnDelete.AutoSize = true;
            btnDelete.Enabled = false;
            btnDelete.Location = new Point(292, 22);
            btnDelete.Margin = new Padding(4, 5, 4, 5);
            btnDelete.Name = "btnDelete";
            btnDelete.Size = new Size(129, 58);
            btnDelete.TabIndex = 2;
            btnDelete.Text = "Delete";
            btnDelete.UseVisualStyleBackColor = true;
            btnDelete.Click += btnDelete_Click;
            // 
            // btnRefresh
            // 
            btnRefresh.AutoSize = true;
            btnRefresh.Location = new Point(429, 22);
            btnRefresh.Margin = new Padding(4, 5, 4, 5);
            btnRefresh.Name = "btnRefresh";
            btnRefresh.Size = new Size(129, 58);
            btnRefresh.TabIndex = 3;
            btnRefresh.Text = "Refresh";
            btnRefresh.UseVisualStyleBackColor = true;
            btnRefresh.Click += btnRefresh_Click;
            // 
            // EngagementForm
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1181, 848);
            Controls.Add(tableLayoutPanelEditors);
            Controls.Add(flowLayoutPanelButtons);
            Controls.Add(gridEngagements);
            Margin = new Padding(4, 5, 4, 5);
            MaximizeBox = false;
            MinimizeBox = false;
            MinimumSize = new Size(1133, 796);
            Name = "EngagementForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Engagements";
            tableLayoutPanelEditors.ResumeLayout(false);
            tableLayoutPanelEditors.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)gridEngagements).EndInit();
            flowLayoutPanelButtons.ResumeLayout(false);
            flowLayoutPanelButtons.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
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
