using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using GRCFinancialControl.Configuration;
using GRCFinancialControl.Data;
using GRCFinancialControl.Services;

namespace GRCFinancialControl.Forms
{
    public partial class MeasurementPeriodForm : Form
    {
        private readonly AppConfig _config;
        private readonly ParametersService _parametersService;
        private readonly BindingList<MeasurementPeriod> _periods = new();
        private MeasurementPeriod? _currentPeriod;
        private bool _isNew = true;
        private bool _suppressEvents;
        private ushort? _activePeriodId;

        public MeasurementPeriodForm(AppConfig config, ParametersService parametersService)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _parametersService = parametersService ?? throw new ArgumentNullException(nameof(parametersService));
            InitializeComponent();
            gridMeasurementPeriods.AutoGenerateColumns = false;
            gridMeasurementPeriods.DataSource = _periods;
            EnterNewMode();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            LoadActivePeriodId();
            RefreshMeasurementPeriods();
        }

        private void LoadActivePeriodId()
        {
            try
            {
                var stored = _parametersService.GetSelectedMeasurePeriodId();
                if (string.IsNullOrWhiteSpace(stored))
                {
                    _activePeriodId = null;
                }
                else if (ushort.TryParse(stored, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                {
                    _activePeriodId = parsed;
                }
                else
                {
                    _parametersService.ClearSelectedMeasurePeriod();
                    _activePeriodId = null;
                    MessageBox.Show(
                        this,
                        "The stored measurement period selection was invalid and has been cleared.",
                        "Measurement Period",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                ShowError($"Failed to read local parameters: {ex.Message}");
                _activePeriodId = null;
            }

            UpdateActivePeriodLabel();
        }

        private void RefreshMeasurementPeriods(ushort? selectPeriodId = null)
        {
            ushort? targetId = selectPeriodId ?? _currentPeriod?.PeriodId ?? _activePeriodId;
            try
            {
                using var context = DbContextFactory.CreateMySqlContext(_config);
                var service = new MeasurementPeriodService(context);
                var data = service.LoadAllPeriods();

                _suppressEvents = true;
                _periods.Clear();
                foreach (var period in data)
                {
                    _periods.Add(period);
                }
                _suppressEvents = false;

                UpdateActivePeriodLabel();

                if (_periods.Count == 0)
                {
                    EnterNewMode();
                    return;
                }

                if (targetId.HasValue && SelectGridRow(targetId.Value))
                {
                    return;
                }

                _suppressEvents = true;
                gridMeasurementPeriods.ClearSelection();
                if (gridMeasurementPeriods.Rows.Count > 0)
                {
                    gridMeasurementPeriods.Rows[0].Selected = true;
                    gridMeasurementPeriods.CurrentCell = gridMeasurementPeriods.Rows[0].Cells[0];
                }
                _suppressEvents = false;
                UpdateButtonStates();
            }
            catch (Exception ex)
            {
                ShowError($"Failed to load measurement periods: {ex.Message}");
            }
        }

        private bool SelectGridRow(ushort periodId)
        {
            _suppressEvents = true;
            foreach (DataGridViewRow row in gridMeasurementPeriods.Rows)
            {
                if (row.DataBoundItem is MeasurementPeriod period && period.PeriodId == periodId)
                {
                    gridMeasurementPeriods.ClearSelection();
                    row.Selected = true;
                    gridMeasurementPeriods.CurrentCell = row.Cells[0];
                    _suppressEvents = false;
                    UpdateButtonStates();
                    return true;
                }
            }

            _suppressEvents = false;
            return false;
        }

        private void EnterNewMode()
        {
            _suppressEvents = true;
            _isNew = true;
            _currentPeriod = null;
            txtDescription.Text = string.Empty;
            var today = DateTime.Today;
            dtpStartDate.Value = today;
            dtpEndDate.Value = today;
            gridMeasurementPeriods.ClearSelection();
            _suppressEvents = false;
            UpdateButtonStates();
        }

        private void PopulateEditors(MeasurementPeriod period)
        {
            _suppressEvents = true;
            _isNew = false;
            _currentPeriod = period;
            txtDescription.Text = period.Description;
            dtpStartDate.Value = period.StartDate.ToDateTime(TimeOnly.MinValue);
            dtpEndDate.Value = period.EndDate.ToDateTime(TimeOnly.MinValue);
            _suppressEvents = false;
            UpdateButtonStates();
        }

        private void gridMeasurementPeriods_SelectionChanged(object sender, EventArgs e)
        {
            if (_suppressEvents)
            {
                return;
            }

            if (gridMeasurementPeriods.SelectedRows.Count == 0)
            {
                return;
            }

            if (gridMeasurementPeriods.SelectedRows[0].DataBoundItem is MeasurementPeriod period)
            {
                PopulateEditors(period);
            }
        }

        private void btnNew_Click(object sender, EventArgs e)
        {
            EnterNewMode();
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            if (!TryGetEditorSnapshot(out var snapshot, out var errorMessage))
            {
                ShowError(errorMessage ?? "Please correct the highlighted fields.");
                return;
            }

            try
            {
                using var context = DbContextFactory.CreateMySqlContext(_config);
                var service = new MeasurementPeriodService(context);
                ushort selectedId;

                if (_isNew)
                {
                    selectedId = service.Insert(snapshot.Description, snapshot.StartDate, snapshot.EndDate);
                }
                else
                {
                    if (_currentPeriod == null)
                    {
                        ShowError("No measurement period is selected.");
                        return;
                    }

                    selectedId = _currentPeriod.PeriodId;
                    var entity = new MeasurementPeriod
                    {
                        PeriodId = selectedId,
                        Description = snapshot.Description,
                        StartDate = snapshot.StartDate,
                        EndDate = snapshot.EndDate
                    };
                    service.Update(entity);
                }

                RefreshMeasurementPeriods(selectedId);
            }
            catch (Exception ex)
            {
                ShowError($"Failed to save measurement period: {ex.Message}");
            }
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            if (_currentPeriod == null)
            {
                return;
            }

            var isActive = _activePeriodId.HasValue && _currentPeriod.PeriodId == _activePeriodId.Value;
            var prompt = isActive
                ? "The selected period is currently active locally. Continue and clear the local selection?"
                : $"Delete measurement period '{_currentPeriod.Description}'?";

            var result = MessageBox.Show(
                this,
                prompt,
                "Confirm Delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (result != DialogResult.Yes)
            {
                return;
            }

            try
            {
                using var context = DbContextFactory.CreateMySqlContext(_config);
                var service = new MeasurementPeriodService(context);
                service.Delete(_currentPeriod.PeriodId);
            }
            catch (Exception ex)
            {
                ShowError($"Failed to delete measurement period: {ex.Message}");
                return;
            }

            if (isActive)
            {
                try
                {
                    _parametersService.ClearSelectedMeasurePeriod();
                }
                catch (Exception ex)
                {
                    ShowError($"Failed to clear local selection: {ex.Message}");
                }

                _activePeriodId = null;
                UpdateActivePeriodLabel();
            }

            EnterNewMode();
            RefreshMeasurementPeriods();
        }

        private void btnActivate_Click(object sender, EventArgs e)
        {
            if (_currentPeriod == null)
            {
                return;
            }

            try
            {
                var idText = _currentPeriod.PeriodId.ToString(CultureInfo.InvariantCulture);
                _parametersService.SetSelectedMeasurePeriodId(idText);
                _activePeriodId = _currentPeriod.PeriodId;
                UpdateActivePeriodLabel();
                MessageBox.Show(
                    this,
                    $"Measurement period '{_currentPeriod.Description}' is now active locally.",
                    "Measurement Period Activated",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                ShowError($"Failed to update local selection: {ex.Message}");
            }
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void EditorChanged(object sender, EventArgs e)
        {
            if (_suppressEvents)
            {
                return;
            }

            UpdateButtonStates();
        }

        private bool TryGetEditorSnapshot(out MeasurementPeriodEditorSnapshot snapshot, out string? errorMessage)
        {
            snapshot = default;
            errorMessage = null;

            var description = txtDescription.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(description))
            {
                errorMessage = "Description is required.";
                return false;
            }

            if (description.Length > 255)
            {
                errorMessage = "Description must be 255 characters or less.";
                return false;
            }

            var start = DateOnly.FromDateTime(dtpStartDate.Value.Date);
            var end = DateOnly.FromDateTime(dtpEndDate.Value.Date);
            if (end < start)
            {
                errorMessage = "End date must be greater than or equal to start date.";
                return false;
            }

            snapshot = new MeasurementPeriodEditorSnapshot(description, start, end);
            return true;
        }

        private void UpdateButtonStates()
        {
            if (_suppressEvents)
            {
                return;
            }

            btnDelete.Enabled = !_isNew && _currentPeriod != null;
            btnActivate.Enabled = !_isNew && _currentPeriod != null;

            if (TryGetEditorSnapshot(out var snapshot, out _))
            {
                btnSave.Enabled = _isNew || _currentPeriod == null || IsDirty(snapshot);
            }
            else
            {
                btnSave.Enabled = false;
            }
        }

        private bool IsDirty(in MeasurementPeriodEditorSnapshot snapshot)
        {
            if (_currentPeriod == null)
            {
                return true;
            }

            if (!string.Equals(_currentPeriod.Description, snapshot.Description, StringComparison.Ordinal))
            {
                return true;
            }

            if (_currentPeriod.StartDate != snapshot.StartDate)
            {
                return true;
            }

            return _currentPeriod.EndDate != snapshot.EndDate;
        }

        private void UpdateActivePeriodLabel()
        {
            if (_activePeriodId.HasValue)
            {
                var match = _periods.FirstOrDefault(p => p.PeriodId == _activePeriodId.Value);
                if (match != null)
                {
                    lblActivePeriod.Text = $"Current Active Period: {match.ToDisplayString()}";
                }
                else
                {
                    lblActivePeriod.Text = $"Current Active Period: ID {_activePeriodId.Value}";
                }
            }
            else
            {
                lblActivePeriod.Text = "Current Active Period: -";
            }
        }

        private void ShowError(string message)
        {
            MessageBox.Show(this, message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private readonly struct MeasurementPeriodEditorSnapshot
        {
            public MeasurementPeriodEditorSnapshot(string description, DateOnly startDate, DateOnly endDate)
            {
                Description = description;
                StartDate = startDate;
                EndDate = endDate;
            }

            public string Description { get; }
            public DateOnly StartDate { get; }
            public DateOnly EndDate { get; }
        }
    }
}
