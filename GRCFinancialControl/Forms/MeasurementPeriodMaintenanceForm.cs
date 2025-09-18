using System;
using System.Linq;
using System.Windows.Forms;
using GRCFinancialControl.Configuration;
using GRCFinancialControl.Data;
using GRCFinancialControl.Services;

namespace GRCFinancialControl.Forms
{
    public partial class MeasurementPeriodMaintenanceForm : Form
    {
        private readonly AppConfig _config;

        public MeasurementPeriodMaintenanceForm(AppConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            InitializeComponent();
            LoadPeriods();
        }

        private void LoadPeriods()
        {
            using var context = DbContextFactory.Create(_config);
            var service = new MeasurementPeriodService(context);
            var periods = service.GetAll();

            lstMeasurementPeriods.BeginUpdate();
            try
            {
                lstMeasurementPeriods.Items.Clear();
                foreach (var period in periods)
                {
                    lstMeasurementPeriods.Items.Add(period);
                }
            }
            finally
            {
                lstMeasurementPeriods.EndUpdate();
            }

            UpdateButtons();
        }

        private void UpdateButtons()
        {
            var selected = GetSelectedPeriod();
            var hasSelection = selected != null;
            btnEdit.Enabled = hasSelection;
            btnDelete.Enabled = hasSelection && selected!.IsActive == false;
            btnActivate.Enabled = hasSelection;
        }

        private DimMeasurementPeriod? GetSelectedPeriod()
        {
            return lstMeasurementPeriods.SelectedItem as DimMeasurementPeriod;
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            using var editor = new MeasurementPeriodEditorForm();
            if (editor.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            try
            {
                using var context = DbContextFactory.Create(_config);
                var service = new MeasurementPeriodService(context);
                var created = service.Create(editor.PeriodDescription, editor.StartDate, editor.EndDate);
                LoadPeriods();
                SelectPeriod(created.MeasurementPeriodId);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to add measurement period: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnEdit_Click(object sender, EventArgs e)
        {
            var selected = GetSelectedPeriod();
            if (selected == null)
            {
                return;
            }

            using var editor = new MeasurementPeriodEditorForm(selected);
            if (editor.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            try
            {
                using var context = DbContextFactory.Create(_config);
                var service = new MeasurementPeriodService(context);
                var updated = service.Update(selected.MeasurementPeriodId, editor.PeriodDescription, editor.StartDate, editor.EndDate);
                LoadPeriods();
                SelectPeriod(updated.MeasurementPeriodId);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to update measurement period: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            var selected = GetSelectedPeriod();
            if (selected == null)
            {
                return;
            }

            var confirm = MessageBox.Show(this, $"Delete measurement period '{selected.Description}'?", "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (confirm != DialogResult.Yes)
            {
                return;
            }

            try
            {
                using var context = DbContextFactory.Create(_config);
                var service = new MeasurementPeriodService(context);
                service.Delete(selected.MeasurementPeriodId);
                LoadPeriods();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to delete measurement period: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnActivate_Click(object sender, EventArgs e)
        {
            var selected = GetSelectedPeriod();
            if (selected == null)
            {
                return;
            }

            try
            {
                using var context = DbContextFactory.Create(_config);
                var service = new MeasurementPeriodService(context);
                var activated = service.Activate(selected.MeasurementPeriodId);
                LoadPeriods();
                SelectPeriod(activated.MeasurementPeriodId);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to activate measurement period: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void lstMeasurementPeriods_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateButtons();
        }

        private void SelectPeriod(ushort id)
        {
            foreach (var item in lstMeasurementPeriods.Items.OfType<DimMeasurementPeriod>())
            {
                if (item.MeasurementPeriodId == id)
                {
                    lstMeasurementPeriods.SelectedItem = item;
                    return;
                }
            }
        }
    }
}
