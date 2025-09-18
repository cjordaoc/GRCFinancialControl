using System;
using System.Windows.Forms;
using GRCFinancialControl.Data;

namespace GRCFinancialControl.Forms
{
    public partial class MeasurementPeriodEditorForm : Form
    {
        public MeasurementPeriodEditorForm(DimMeasurementPeriod? period = null)
        {
            InitializeComponent();
            if (period != null)
            {
                txtDescription.Text = period.Description;
                dtpStartDate.Value = period.StartDate.ToDateTime(TimeOnly.MinValue);
                dtpEndDate.Value = period.EndDate.ToDateTime(TimeOnly.MinValue);
            }
            else
            {
                var today = DateTime.Today;
                dtpStartDate.Value = today;
                dtpEndDate.Value = today;
            }
        }

        public string PeriodDescription => txtDescription.Text.Trim();

        public DateOnly StartDate => DateOnly.FromDateTime(dtpStartDate.Value.Date);

        public DateOnly EndDate => DateOnly.FromDateTime(dtpEndDate.Value.Date);

        private void btnOk_Click(object sender, EventArgs e)
        {
            var description = txtDescription.Text?.Trim();
            if (string.IsNullOrWhiteSpace(description))
            {
                MessageBox.Show(this, "Description is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtDescription.Focus();
                return;
            }

            if (dtpEndDate.Value.Date < dtpStartDate.Value.Date)
            {
                MessageBox.Show(this, "End date must be on or after the start date.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                dtpEndDate.Focus();
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
