using System;
using System.Windows.Forms;
using GRCFinancialControl.Data;

namespace GRCFinancialControl.Forms
{
    public partial class FiscalYearEditorForm : Form
    {
        public FiscalYearEditorForm(DimFiscalYear? fiscalYear = null)
        {
            InitializeComponent();
            if (fiscalYear != null)
            {
                txtDescription.Text = fiscalYear.Description;
                dtpDateFrom.Value = fiscalYear.DateFrom.ToDateTime(TimeOnly.MinValue);
                dtpDateTo.Value = fiscalYear.DateTo.ToDateTime(TimeOnly.MinValue);
            }
            else
            {
                var today = DateTime.Today;
                dtpDateFrom.Value = today;
                dtpDateTo.Value = today;
            }
        }

        public string FiscalYearDescription => txtDescription.Text.Trim();

        public DateOnly DateFrom => DateOnly.FromDateTime(dtpDateFrom.Value.Date);

        public DateOnly DateTo => DateOnly.FromDateTime(dtpDateTo.Value.Date);

        private void btnOk_Click(object sender, EventArgs e)
        {
            var description = txtDescription.Text?.Trim();
            if (string.IsNullOrWhiteSpace(description))
            {
                MessageBox.Show(this, "Description is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtDescription.Focus();
                return;
            }

            if (dtpDateTo.Value.Date < dtpDateFrom.Value.Date)
            {
                MessageBox.Show(this, "End date must be on or after the start date.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                dtpDateTo.Focus();
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
