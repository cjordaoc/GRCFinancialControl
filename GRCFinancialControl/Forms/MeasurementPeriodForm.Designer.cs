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
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle4 = new System.Windows.Forms.DataGridViewCellStyle();
            this.tableLayoutPanelEditors = new System.Windows.Forms.TableLayoutPanel();
            this.lblDescription = new System.Windows.Forms.Label();
            this.txtDescription = new System.Windows.Forms.TextBox();
            this.lblStartDate = new System.Windows.Forms.Label();
            this.dtpStartDate = new System.Windows.Forms.DateTimePicker();
            this.lblEndDate = new System.Windows.Forms.Label();
            this.dtpEndDate = new System.Windows.Forms.DateTimePicker();
            this.lblActivePeriod = new System.Windows.Forms.Label();
            this.gridMeasurementPeriods = new System.Windows.Forms.DataGridView();
            this.colPeriodId = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colDescription = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colStartDate = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colEndDate = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colCreatedUtc = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colUpdatedUtc = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.flowLayoutPanelButtons = new System.Windows.Forms.FlowLayoutPanel();
            this.btnNew = new System.Windows.Forms.Button();
            this.btnSave = new System.Windows.Forms.Button();
            this.btnDelete = new System.Windows.Forms.Button();
            this.btnActivate = new System.Windows.Forms.Button();
            this.btnClose = new System.Windows.Forms.Button();
            this.tableLayoutPanelEditors.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridMeasurementPeriods)).BeginInit();
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
            this.tableLayoutPanelEditors.Controls.Add(this.lblDescription, 0, 0);
            this.tableLayoutPanelEditors.Controls.Add(this.txtDescription, 1, 0);
            this.tableLayoutPanelEditors.Controls.Add(this.lblStartDate, 0, 1);
            this.tableLayoutPanelEditors.Controls.Add(this.dtpStartDate, 1, 1);
            this.tableLayoutPanelEditors.Controls.Add(this.lblEndDate, 0, 2);
            this.tableLayoutPanelEditors.Controls.Add(this.dtpEndDate, 1, 2);
            this.tableLayoutPanelEditors.Dock = System.Windows.Forms.DockStyle.Top;
            this.tableLayoutPanelEditors.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanelEditors.Margin = new System.Windows.Forms.Padding(6);
            this.tableLayoutPanelEditors.Name = "tableLayoutPanelEditors";
            this.tableLayoutPanelEditors.Padding = new System.Windows.Forms.Padding(10, 10, 10, 0);
            this.tableLayoutPanelEditors.RowCount = 3;
            this.tableLayoutPanelEditors.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanelEditors.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanelEditors.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanelEditors.Size = new System.Drawing.Size(824, 116);
            this.tableLayoutPanelEditors.TabIndex = 0;
            // 
            // lblDescription
            // 
            this.lblDescription.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblDescription.AutoSize = true;
            this.lblDescription.Location = new System.Drawing.Point(13, 15);
            this.lblDescription.Margin = new System.Windows.Forms.Padding(3, 5, 3, 5);
            this.lblDescription.Name = "lblDescription";
            this.lblDescription.Size = new System.Drawing.Size(69, 15);
            this.lblDescription.TabIndex = 0;
            this.lblDescription.Text = "Description";
            // 
            // txtDescription
            // 
            this.txtDescription.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtDescription.Location = new System.Drawing.Point(122, 13);
            this.txtDescription.Margin = new System.Windows.Forms.Padding(3, 3, 10, 3);
            this.txtDescription.MaxLength = 255;
            this.txtDescription.Name = "txtDescription";
            this.txtDescription.Size = new System.Drawing.Size(682, 23);
            this.txtDescription.TabIndex = 1;
            this.txtDescription.TextChanged += new System.EventHandler(this.EditorChanged);
            // 
            // lblStartDate
            // 
            this.lblStartDate.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblStartDate.AutoSize = true;
            this.lblStartDate.Location = new System.Drawing.Point(13, 48);
            this.lblStartDate.Margin = new System.Windows.Forms.Padding(3, 5, 3, 5);
            this.lblStartDate.Name = "lblStartDate";
            this.lblStartDate.Size = new System.Drawing.Size(61, 15);
            this.lblStartDate.TabIndex = 2;
            this.lblStartDate.Text = "Start Date";
            // 
            // dtpStartDate
            // 
            this.dtpStartDate.Format = System.Windows.Forms.DateTimePickerFormat.Short;
            this.dtpStartDate.Location = new System.Drawing.Point(122, 45);
            this.dtpStartDate.Margin = new System.Windows.Forms.Padding(3, 3, 10, 3);
            this.dtpStartDate.Name = "dtpStartDate";
            this.dtpStartDate.Size = new System.Drawing.Size(140, 23);
            this.dtpStartDate.TabIndex = 3;
            this.dtpStartDate.ValueChanged += new System.EventHandler(this.EditorChanged);
            // 
            // lblEndDate
            // 
            this.lblEndDate.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblEndDate.AutoSize = true;
            this.lblEndDate.Location = new System.Drawing.Point(13, 81);
            this.lblEndDate.Margin = new System.Windows.Forms.Padding(3, 5, 3, 5);
            this.lblEndDate.Name = "lblEndDate";
            this.lblEndDate.Size = new System.Drawing.Size(55, 15);
            this.lblEndDate.TabIndex = 4;
            this.lblEndDate.Text = "End Date";
            // 
            // dtpEndDate
            // 
            this.dtpEndDate.Format = System.Windows.Forms.DateTimePickerFormat.Short;
            this.dtpEndDate.Location = new System.Drawing.Point(122, 78);
            this.dtpEndDate.Margin = new System.Windows.Forms.Padding(3, 3, 10, 3);
            this.dtpEndDate.Name = "dtpEndDate";
            this.dtpEndDate.Size = new System.Drawing.Size(140, 23);
            this.dtpEndDate.TabIndex = 5;
            this.dtpEndDate.ValueChanged += new System.EventHandler(this.EditorChanged);
            // 
            // lblActivePeriod
            // 
            this.lblActivePeriod.AutoSize = true;
            this.lblActivePeriod.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblActivePeriod.Location = new System.Drawing.Point(0, 116);
            this.lblActivePeriod.Margin = new System.Windows.Forms.Padding(10);
            this.lblActivePeriod.Name = "lblActivePeriod";
            this.lblActivePeriod.Padding = new System.Windows.Forms.Padding(10, 5, 10, 5);
            this.lblActivePeriod.Size = new System.Drawing.Size(141, 25);
            this.lblActivePeriod.TabIndex = 1;
            this.lblActivePeriod.Text = "Current Active Period: -";
            // 
            // gridMeasurementPeriods
            // 
            this.gridMeasurementPeriods.AllowUserToAddRows = false;
            this.gridMeasurementPeriods.AllowUserToDeleteRows = false;
            this.gridMeasurementPeriods.AllowUserToResizeRows = false;
            this.gridMeasurementPeriods.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.gridMeasurementPeriods.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridMeasurementPeriods.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colPeriodId,
            this.colDescription,
            this.colStartDate,
            this.colEndDate,
            this.colCreatedUtc,
            this.colUpdatedUtc});
            this.gridMeasurementPeriods.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridMeasurementPeriods.Location = new System.Drawing.Point(0, 141);
            this.gridMeasurementPeriods.MultiSelect = false;
            this.gridMeasurementPeriods.Name = "gridMeasurementPeriods";
            this.gridMeasurementPeriods.ReadOnly = true;
            this.gridMeasurementPeriods.RowHeadersVisible = false;
            this.gridMeasurementPeriods.RowTemplate.Height = 25;
            this.gridMeasurementPeriods.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.gridMeasurementPeriods.Size = new System.Drawing.Size(824, 309);
            this.gridMeasurementPeriods.TabIndex = 2;
            this.gridMeasurementPeriods.SelectionChanged += new System.EventHandler(this.gridMeasurementPeriods_SelectionChanged);
            // 
            // colPeriodId
            // 
            this.colPeriodId.DataPropertyName = "PeriodId";
            this.colPeriodId.HeaderText = "ID";
            this.colPeriodId.MinimumWidth = 60;
            this.colPeriodId.Name = "colPeriodId";
            this.colPeriodId.ReadOnly = true;
            // 
            // colDescription
            // 
            this.colDescription.DataPropertyName = "Description";
            this.colDescription.HeaderText = "Description";
            this.colDescription.MinimumWidth = 150;
            this.colDescription.Name = "colDescription";
            this.colDescription.ReadOnly = true;
            // 
            // colStartDate
            // 
            this.colStartDate.DataPropertyName = "StartDate";
            this.colStartDate.HeaderText = "Start Date";
            this.colStartDate.MinimumWidth = 100;
            this.colStartDate.Name = "colStartDate";
            this.colStartDate.ReadOnly = true;
            dataGridViewCellStyle1.Format = "yyyy-MM-dd";
            dataGridViewCellStyle1.NullValue = null;
            this.colStartDate.DefaultCellStyle = dataGridViewCellStyle1;
            // 
            // colEndDate
            // 
            this.colEndDate.DataPropertyName = "EndDate";
            this.colEndDate.HeaderText = "End Date";
            this.colEndDate.MinimumWidth = 100;
            this.colEndDate.Name = "colEndDate";
            this.colEndDate.ReadOnly = true;
            dataGridViewCellStyle2.Format = "yyyy-MM-dd";
            dataGridViewCellStyle2.NullValue = null;
            this.colEndDate.DefaultCellStyle = dataGridViewCellStyle2;
            // 
            // colCreatedUtc
            // 
            this.colCreatedUtc.DataPropertyName = "CreatedUtc";
            this.colCreatedUtc.HeaderText = "Created (UTC)";
            this.colCreatedUtc.MinimumWidth = 120;
            this.colCreatedUtc.Name = "colCreatedUtc";
            this.colCreatedUtc.ReadOnly = true;
            dataGridViewCellStyle3.Format = "g";
            dataGridViewCellStyle3.NullValue = null;
            this.colCreatedUtc.DefaultCellStyle = dataGridViewCellStyle3;
            // 
            // colUpdatedUtc
            // 
            this.colUpdatedUtc.DataPropertyName = "UpdatedUtc";
            this.colUpdatedUtc.HeaderText = "Updated (UTC)";
            this.colUpdatedUtc.MinimumWidth = 120;
            this.colUpdatedUtc.Name = "colUpdatedUtc";
            this.colUpdatedUtc.ReadOnly = true;
            dataGridViewCellStyle4.Format = "g";
            dataGridViewCellStyle4.NullValue = null;
            this.colUpdatedUtc.DefaultCellStyle = dataGridViewCellStyle4;
            // 
            // flowLayoutPanelButtons
            // 
            this.flowLayoutPanelButtons.AutoSize = true;
            this.flowLayoutPanelButtons.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.flowLayoutPanelButtons.Controls.Add(this.btnNew);
            this.flowLayoutPanelButtons.Controls.Add(this.btnSave);
            this.flowLayoutPanelButtons.Controls.Add(this.btnDelete);
            this.flowLayoutPanelButtons.Controls.Add(this.btnActivate);
            this.flowLayoutPanelButtons.Controls.Add(this.btnClose);
            this.flowLayoutPanelButtons.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.flowLayoutPanelButtons.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
            this.flowLayoutPanelButtons.Location = new System.Drawing.Point(0, 450);
            this.flowLayoutPanelButtons.Margin = new System.Windows.Forms.Padding(10);
            this.flowLayoutPanelButtons.Name = "flowLayoutPanelButtons";
            this.flowLayoutPanelButtons.Padding = new System.Windows.Forms.Padding(10);
            this.flowLayoutPanelButtons.Size = new System.Drawing.Size(824, 60);
            this.flowLayoutPanelButtons.TabIndex = 3;
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
            // btnActivate
            // 
            this.btnActivate.AutoSize = true;
            this.btnActivate.Enabled = false;
            this.btnActivate.Location = new System.Drawing.Point(301, 13);
            this.btnActivate.Margin = new System.Windows.Forms.Padding(3);
            this.btnActivate.Name = "btnActivate";
            this.btnActivate.Size = new System.Drawing.Size(90, 30);
            this.btnActivate.TabIndex = 3;
            this.btnActivate.Text = "Activate";
            this.btnActivate.UseVisualStyleBackColor = true;
            this.btnActivate.Click += new System.EventHandler(this.btnActivate_Click);
            // 
            // btnClose
            // 
            this.btnClose.AutoSize = true;
            this.btnClose.Location = new System.Drawing.Point(397, 13);
            this.btnClose.Margin = new System.Windows.Forms.Padding(3);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(90, 30);
            this.btnClose.TabIndex = 4;
            this.btnClose.Text = "Close";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
            // 
            // MeasurementPeriodForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(824, 510);
            this.Controls.Add(this.tableLayoutPanelEditors);
            this.Controls.Add(this.lblActivePeriod);
            this.Controls.Add(this.flowLayoutPanelButtons);
            this.Controls.Add(this.gridMeasurementPeriods);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(720, 480);
            this.Name = "MeasurementPeriodForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Measurement Periods";
            this.tableLayoutPanelEditors.ResumeLayout(false);
            this.tableLayoutPanelEditors.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridMeasurementPeriods)).EndInit();
            this.flowLayoutPanelButtons.ResumeLayout(false);
            this.flowLayoutPanelButtons.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
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
