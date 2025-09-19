namespace GRCFinancialControl.Forms
{
    partial class MeasurementPeriodForm
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
            DataGridViewCellStyle dataGridViewCellStyle5 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle6 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle7 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle8 = new DataGridViewCellStyle();
            tableLayoutPanelEditors = new TableLayoutPanel();
            lblDescription = new Label();
            txtDescription = new TextBox();
            lblStartDate = new Label();
            dtpStartDate = new DateTimePicker();
            lblEndDate = new Label();
            dtpEndDate = new DateTimePicker();
            lblActivePeriod = new Label();
            gridMeasurementPeriods = new DataGridView();
            colPeriodId = new DataGridViewTextBoxColumn();
            colDescription = new DataGridViewTextBoxColumn();
            colStartDate = new DataGridViewTextBoxColumn();
            colEndDate = new DataGridViewTextBoxColumn();
            colCreatedUtc = new DataGridViewTextBoxColumn();
            colUpdatedUtc = new DataGridViewTextBoxColumn();
            flowLayoutPanelButtons = new FlowLayoutPanel();
            btnNew = new Button();
            btnSave = new Button();
            btnDelete = new Button();
            btnActivate = new Button();
            btnClose = new Button();
            tableLayoutPanelEditors.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)gridMeasurementPeriods).BeginInit();
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
            tableLayoutPanelEditors.Controls.Add(lblDescription, 0, 0);
            tableLayoutPanelEditors.Controls.Add(txtDescription, 1, 0);
            tableLayoutPanelEditors.Controls.Add(lblStartDate, 0, 1);
            tableLayoutPanelEditors.Controls.Add(dtpStartDate, 1, 1);
            tableLayoutPanelEditors.Controls.Add(lblEndDate, 0, 2);
            tableLayoutPanelEditors.Controls.Add(dtpEndDate, 1, 2);
            tableLayoutPanelEditors.Dock = DockStyle.Bottom;
            tableLayoutPanelEditors.Location = new Point(0, 93);
            tableLayoutPanelEditors.Margin = new Padding(9, 10, 9, 10);
            tableLayoutPanelEditors.Name = "tableLayoutPanelEditors";
            tableLayoutPanelEditors.Padding = new Padding(14, 17, 14, 0);
            tableLayoutPanelEditors.RowCount = 3;
            tableLayoutPanelEditors.RowStyles.Add(new RowStyle());
            tableLayoutPanelEditors.RowStyles.Add(new RowStyle());
            tableLayoutPanelEditors.RowStyles.Add(new RowStyle());
            tableLayoutPanelEditors.Size = new Size(1177, 140);
            tableLayoutPanelEditors.TabIndex = 0;
            // 
            // lblDescription
            // 
            lblDescription.Anchor = AnchorStyles.Left;
            lblDescription.AutoSize = true;
            lblDescription.Location = new Point(18, 25);
            lblDescription.Margin = new Padding(4, 8, 4, 8);
            lblDescription.Name = "lblDescription";
            lblDescription.Size = new Size(102, 25);
            lblDescription.TabIndex = 0;
            lblDescription.Text = "Description";
            // 
            // txtDescription
            // 
            txtDescription.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtDescription.Location = new Point(128, 22);
            txtDescription.Margin = new Padding(4, 5, 14, 5);
            txtDescription.MaxLength = 255;
            txtDescription.Name = "txtDescription";
            txtDescription.Size = new Size(1021, 31);
            txtDescription.TabIndex = 1;
            txtDescription.TextChanged += EditorChanged;
            // 
            // lblStartDate
            // 
            lblStartDate.Anchor = AnchorStyles.Left;
            lblStartDate.AutoSize = true;
            lblStartDate.Location = new Point(18, 66);
            lblStartDate.Margin = new Padding(4, 8, 4, 8);
            lblStartDate.Name = "lblStartDate";
            lblStartDate.Size = new Size(90, 25);
            lblStartDate.TabIndex = 2;
            lblStartDate.Text = "Start Date";
            // 
            // dtpStartDate
            // 
            dtpStartDate.Format = DateTimePickerFormat.Short;
            dtpStartDate.Location = new Point(128, 63);
            dtpStartDate.Margin = new Padding(4, 5, 14, 5);
            dtpStartDate.Name = "dtpStartDate";
            dtpStartDate.Size = new Size(198, 31);
            dtpStartDate.TabIndex = 3;
            dtpStartDate.ValueChanged += EditorChanged;
            // 
            // lblEndDate
            // 
            lblEndDate.Anchor = AnchorStyles.Left;
            lblEndDate.AutoSize = true;
            lblEndDate.Location = new Point(18, 107);
            lblEndDate.Margin = new Padding(4, 8, 4, 8);
            lblEndDate.Name = "lblEndDate";
            lblEndDate.Size = new Size(84, 25);
            lblEndDate.TabIndex = 4;
            lblEndDate.Text = "End Date";
            // 
            // dtpEndDate
            // 
            dtpEndDate.Format = DateTimePickerFormat.Short;
            dtpEndDate.Location = new Point(128, 104);
            dtpEndDate.Margin = new Padding(4, 5, 14, 5);
            dtpEndDate.Name = "dtpEndDate";
            dtpEndDate.Size = new Size(198, 31);
            dtpEndDate.TabIndex = 5;
            dtpEndDate.ValueChanged += EditorChanged;
            // 
            // lblActivePeriod
            // 
            lblActivePeriod.AutoSize = true;
            lblActivePeriod.Dock = DockStyle.Top;
            lblActivePeriod.Location = new Point(0, 0);
            lblActivePeriod.Margin = new Padding(14, 17, 14, 17);
            lblActivePeriod.Name = "lblActivePeriod";
            lblActivePeriod.Padding = new Padding(14, 8, 14, 8);
            lblActivePeriod.Size = new Size(222, 41);
            lblActivePeriod.TabIndex = 1;
            lblActivePeriod.Text = "Current Active Period: -";
            // 
            // gridMeasurementPeriods
            // 
            gridMeasurementPeriods.AllowUserToAddRows = false;
            gridMeasurementPeriods.AllowUserToDeleteRows = false;
            gridMeasurementPeriods.AllowUserToResizeRows = false;
            gridMeasurementPeriods.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            gridMeasurementPeriods.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            gridMeasurementPeriods.Columns.AddRange(new DataGridViewColumn[] { colPeriodId, colDescription, colStartDate, colEndDate, colCreatedUtc, colUpdatedUtc });
            gridMeasurementPeriods.Dock = DockStyle.Bottom;
            gridMeasurementPeriods.Location = new Point(0, 335);
            gridMeasurementPeriods.Margin = new Padding(4, 5, 4, 5);
            gridMeasurementPeriods.MultiSelect = false;
            gridMeasurementPeriods.Name = "gridMeasurementPeriods";
            gridMeasurementPeriods.ReadOnly = true;
            gridMeasurementPeriods.RowHeadersVisible = false;
            gridMeasurementPeriods.RowHeadersWidth = 62;
            gridMeasurementPeriods.RowTemplate.Height = 25;
            gridMeasurementPeriods.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            gridMeasurementPeriods.Size = new Size(1177, 515);
            gridMeasurementPeriods.TabIndex = 2;
            gridMeasurementPeriods.SelectionChanged += gridMeasurementPeriods_SelectionChanged;
            // 
            // colPeriodId
            // 
            colPeriodId.DataPropertyName = "PeriodId";
            colPeriodId.HeaderText = "ID";
            colPeriodId.MinimumWidth = 60;
            colPeriodId.Name = "colPeriodId";
            colPeriodId.ReadOnly = true;
            // 
            // colDescription
            // 
            colDescription.DataPropertyName = "Description";
            colDescription.HeaderText = "Description";
            colDescription.MinimumWidth = 150;
            colDescription.Name = "colDescription";
            colDescription.ReadOnly = true;
            // 
            // colStartDate
            // 
            colStartDate.DataPropertyName = "StartDate";
            dataGridViewCellStyle5.Format = "yyyy-MM-dd";
            dataGridViewCellStyle5.NullValue = null;
            colStartDate.DefaultCellStyle = dataGridViewCellStyle5;
            colStartDate.HeaderText = "Start Date";
            colStartDate.MinimumWidth = 100;
            colStartDate.Name = "colStartDate";
            colStartDate.ReadOnly = true;
            // 
            // colEndDate
            // 
            colEndDate.DataPropertyName = "EndDate";
            dataGridViewCellStyle6.Format = "yyyy-MM-dd";
            dataGridViewCellStyle6.NullValue = null;
            colEndDate.DefaultCellStyle = dataGridViewCellStyle6;
            colEndDate.HeaderText = "End Date";
            colEndDate.MinimumWidth = 100;
            colEndDate.Name = "colEndDate";
            colEndDate.ReadOnly = true;
            // 
            // colCreatedUtc
            // 
            colCreatedUtc.DataPropertyName = "CreatedUtc";
            dataGridViewCellStyle7.Format = "g";
            dataGridViewCellStyle7.NullValue = null;
            colCreatedUtc.DefaultCellStyle = dataGridViewCellStyle7;
            colCreatedUtc.HeaderText = "Created (UTC)";
            colCreatedUtc.MinimumWidth = 120;
            colCreatedUtc.Name = "colCreatedUtc";
            colCreatedUtc.ReadOnly = true;
            // 
            // colUpdatedUtc
            // 
            colUpdatedUtc.DataPropertyName = "UpdatedUtc";
            dataGridViewCellStyle8.Format = "g";
            dataGridViewCellStyle8.NullValue = null;
            colUpdatedUtc.DefaultCellStyle = dataGridViewCellStyle8;
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
            flowLayoutPanelButtons.Controls.Add(btnActivate);
            flowLayoutPanelButtons.Controls.Add(btnClose);
            flowLayoutPanelButtons.Dock = DockStyle.Bottom;
            flowLayoutPanelButtons.Location = new Point(0, 233);
            flowLayoutPanelButtons.Margin = new Padding(14, 17, 14, 17);
            flowLayoutPanelButtons.Name = "flowLayoutPanelButtons";
            flowLayoutPanelButtons.Padding = new Padding(14, 17, 14, 17);
            flowLayoutPanelButtons.Size = new Size(1177, 102);
            flowLayoutPanelButtons.TabIndex = 3;
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
            // btnActivate
            // 
            btnActivate.AutoSize = true;
            btnActivate.Enabled = false;
            btnActivate.Location = new Point(429, 22);
            btnActivate.Margin = new Padding(4, 5, 4, 5);
            btnActivate.Name = "btnActivate";
            btnActivate.Size = new Size(129, 58);
            btnActivate.TabIndex = 3;
            btnActivate.Text = "Activate";
            btnActivate.UseVisualStyleBackColor = true;
            btnActivate.Click += btnActivate_Click;
            // 
            // btnClose
            // 
            btnClose.AutoSize = true;
            btnClose.Location = new Point(566, 22);
            btnClose.Margin = new Padding(4, 5, 4, 5);
            btnClose.Name = "btnClose";
            btnClose.Size = new Size(129, 58);
            btnClose.TabIndex = 4;
            btnClose.Text = "Close";
            btnClose.UseVisualStyleBackColor = true;
            btnClose.Click += btnClose_Click;
            // 
            // MeasurementPeriodForm
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1177, 850);
            Controls.Add(tableLayoutPanelEditors);
            Controls.Add(lblActivePeriod);
            Controls.Add(flowLayoutPanelButtons);
            Controls.Add(gridMeasurementPeriods);
            Margin = new Padding(4, 5, 4, 5);
            MaximizeBox = false;
            MinimizeBox = false;
            MinimumSize = new Size(1019, 763);
            Name = "MeasurementPeriodForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Measurement Periods";
            tableLayoutPanelEditors.ResumeLayout(false);
            tableLayoutPanelEditors.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)gridMeasurementPeriods).EndInit();
            flowLayoutPanelButtons.ResumeLayout(false);
            flowLayoutPanelButtons.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelEditors;
        private System.Windows.Forms.Label lblDescription;
        private System.Windows.Forms.TextBox txtDescription;
        private System.Windows.Forms.Label lblStartDate;
        private System.Windows.Forms.DateTimePicker dtpStartDate;
        private System.Windows.Forms.Label lblEndDate;
        private System.Windows.Forms.DateTimePicker dtpEndDate;
        private System.Windows.Forms.Label lblActivePeriod;
        private System.Windows.Forms.DataGridView gridMeasurementPeriods;
        private System.Windows.Forms.DataGridViewTextBoxColumn colPeriodId;
        private System.Windows.Forms.DataGridViewTextBoxColumn colDescription;
        private System.Windows.Forms.DataGridViewTextBoxColumn colStartDate;
        private System.Windows.Forms.DataGridViewTextBoxColumn colEndDate;
        private System.Windows.Forms.DataGridViewTextBoxColumn colCreatedUtc;
        private System.Windows.Forms.DataGridViewTextBoxColumn colUpdatedUtc;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanelButtons;
        private System.Windows.Forms.Button btnNew;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.Button btnDelete;
        private System.Windows.Forms.Button btnActivate;
        private System.Windows.Forms.Button btnClose;
    }
}
