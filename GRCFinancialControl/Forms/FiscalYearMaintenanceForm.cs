using System;
using System.Linq;
using System.Windows.Forms;
using GRCFinancialControl.Configuration;
using GRCFinancialControl.Data;
using GRCFinancialControl.Services;

namespace GRCFinancialControl.Forms
{
    public partial class FiscalYearMaintenanceForm : Form
    {
        private readonly AppConfig _config;

        public FiscalYearMaintenanceForm(AppConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            InitializeComponent();
            LoadFiscalYears();
        }

        private void LoadFiscalYears()
        {
            using var context = DbContextFactory.CreateMySqlContext(_config);
            var service = new FiscalYearService(context);
            var years = service.GetAll();

            lstFiscalYears.BeginUpdate();
            try
            {
                lstFiscalYears.Items.Clear();
                foreach (var year in years)
                {
                    lstFiscalYears.Items.Add(year);
                }
            }
            finally
            {
                lstFiscalYears.EndUpdate();
            }

            UpdateButtons();
        }

        private void UpdateButtons()
        {
            var selected = GetSelectedFiscalYear();
            var hasSelection = selected != null;
            btnEdit.Enabled = hasSelection;
            btnDelete.Enabled = hasSelection && selected!.IsActive == false;
            btnActivate.Enabled = hasSelection;
        }

        private DimFiscalYear? GetSelectedFiscalYear()
        {
            return lstFiscalYears.SelectedItem as DimFiscalYear;
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            using var editor = new FiscalYearEditorForm();
            if (editor.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            try
            {
                using var context = DbContextFactory.CreateMySqlContext(_config);
                var service = new FiscalYearService(context);
                var created = service.Create(editor.FiscalYearDescription, editor.DateFrom, editor.DateTo);
                LoadFiscalYears();
                SelectFiscalYear(created.FiscalYearId);
                MessageBox.Show(
                    this,
                    $"Fiscal year '{created.Description}' was created successfully.",
                    "Fiscal Year Saved",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to add fiscal year: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnEdit_Click(object sender, EventArgs e)
        {
            var selected = GetSelectedFiscalYear();
            if (selected == null)
            {
                return;
            }

            using var editor = new FiscalYearEditorForm(selected);
            if (editor.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            try
            {
                using var context = DbContextFactory.CreateMySqlContext(_config);
                var service = new FiscalYearService(context);
                var updated = service.Update(selected.FiscalYearId, editor.FiscalYearDescription, editor.DateFrom, editor.DateTo);
                LoadFiscalYears();
                SelectFiscalYear(updated.FiscalYearId);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to update fiscal year: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            var selected = GetSelectedFiscalYear();
            if (selected == null)
            {
                return;
            }

            var confirm = MessageBox.Show(this, $"Delete fiscal year '{selected.Description}'?", "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (confirm != DialogResult.Yes)
            {
                return;
            }

            try
            {
                using var context = DbContextFactory.CreateMySqlContext(_config);
                var service = new FiscalYearService(context);
                service.Delete(selected.FiscalYearId);
                LoadFiscalYears();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to delete fiscal year: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnActivate_Click(object sender, EventArgs e)
        {
            var selected = GetSelectedFiscalYear();
            if (selected == null)
            {
                return;
            }

            try
            {
                using var context = DbContextFactory.CreateMySqlContext(_config);
                var service = new FiscalYearService(context);
                var activated = service.Activate(selected.FiscalYearId);
                LoadFiscalYears();
                SelectFiscalYear(activated.FiscalYearId);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to activate fiscal year: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void lstFiscalYears_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateButtons();
        }

        private void SelectFiscalYear(long id)
        {
            foreach (var item in lstFiscalYears.Items.OfType<DimFiscalYear>())
            {
                if (item.FiscalYearId == id)
                {
                    lstFiscalYears.SelectedItem = item;
                    return;
                }
            }
        }
    }
}
