using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Forms;
using GRCFinancialControl.Configuration;
using GRCFinancialControl.Data;
using GRCFinancialControl.Services;

namespace GRCFinancialControl.Forms
{
    public partial class EngagementForm : Form
    {
        private readonly AppConfig _config;
        private readonly BindingList<EngagementDto> _engagements = new();
        private readonly BindingSource _engagementBindingSource = new();
        private EngagementDto? _currentEngagement;
        private bool _isNew = true;
        private bool _suppressEvents;

        public EngagementForm(AppConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            InitializeComponent();
            gridEngagements.AutoGenerateColumns = false;
            _engagementBindingSource.DataSource = _engagements;
            gridEngagements.DataSource = _engagementBindingSource;
            EnterNewMode();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            RefreshEngagements();
        }

        private void RefreshEngagements(string? selectEngagementId = null)
        {
            string? targetId = selectEngagementId ?? _currentEngagement?.EngagementId;
            try
            {
                using var context = DbContextFactory.CreateMySqlContext(_config);
                var service = new EngagementService(context);
                var data = service.LoadAllEngagements();

                _suppressEvents = true;
                _engagementBindingSource.SuspendBinding();
                try
                {
                    _engagements.Clear();
                    foreach (var engagement in data)
                    {
                        _engagements.Add(engagement);
                    }
                }
                finally
                {
                    _engagementBindingSource.ResumeBinding();
                    _engagementBindingSource.ResetBindings(false);
                    _suppressEvents = false;
                }

                if (_engagements.Count == 0)
                {
                    EnterNewMode();
                    return;
                }

                if (!string.IsNullOrEmpty(targetId) && SelectGridRow(targetId))
                {
                    gridEngagements.Refresh();
                    return;
                }

                _suppressEvents = true;
                gridEngagements.ClearSelection();
                if (gridEngagements.Rows.Count > 0)
                {
                    gridEngagements.Rows[0].Selected = true;
                    gridEngagements.CurrentCell = gridEngagements.Rows[0].Cells[0];
                }
                _suppressEvents = false;
                gridEngagements.Refresh();
                UpdateButtonStates();
            }
            catch (Exception ex)
            {
                ShowError($"Failed to load engagements: {ex.Message}");
            }
        }

        private bool SelectGridRow(string engagementId)
        {
            if (string.IsNullOrEmpty(engagementId))
            {
                return false;
            }

            _suppressEvents = true;
            foreach (DataGridViewRow row in gridEngagements.Rows)
            {
                if (row.DataBoundItem is EngagementDto dto &&
                    string.Equals(dto.EngagementId, engagementId, StringComparison.OrdinalIgnoreCase))
                {
                    gridEngagements.ClearSelection();
                    row.Selected = true;
                    gridEngagements.CurrentCell = row.Cells[0];
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
            _currentEngagement = null;
            txtEngagementId.ReadOnly = false;
            txtEngagementId.Text = string.Empty;
            txtEngagementTitle.Text = string.Empty;
            txtEngagementPartner.Text = string.Empty;
            txtEngagementManager.Text = string.Empty;
            txtOpeningMargin.Text = "0.000";
            gridEngagements.ClearSelection();
            _suppressEvents = false;
            UpdateButtonStates();
        }

        private void PopulateEditors(EngagementDto dto)
        {
            _suppressEvents = true;
            _isNew = false;
            _currentEngagement = dto;
            txtEngagementId.ReadOnly = true;
            txtEngagementId.Text = dto.EngagementId;
            txtEngagementTitle.Text = dto.EngagementTitle;
            txtEngagementPartner.Text = dto.EngagementPartner ?? string.Empty;
            txtEngagementManager.Text = dto.EngagementManager ?? string.Empty;
            txtOpeningMargin.Text = dto.OpeningMargin.ToString("F3", CultureInfo.InvariantCulture);
            _suppressEvents = false;
            UpdateButtonStates();
        }

        private void gridEngagements_SelectionChanged(object sender, EventArgs e)
        {
            if (_suppressEvents)
            {
                return;
            }

            if (gridEngagements.SelectedRows.Count == 0)
            {
                return;
            }

            if (gridEngagements.SelectedRows[0].DataBoundItem is EngagementDto dto)
            {
                PopulateEditors(dto);
            }
        }

        private void btnNew_Click(object sender, EventArgs e)
        {
            EnterNewMode();
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            RefreshEngagements();
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            if (_currentEngagement == null)
            {
                return;
            }

            var confirm = MessageBox.Show(
                this,
                $"Delete engagement '{_currentEngagement.EngagementId}'?",
                "Confirm Delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (confirm != DialogResult.Yes)
            {
                return;
            }

            try
            {
                using var context = DbContextFactory.CreateMySqlContext(_config);
                var service = new EngagementService(context);
                service.Delete(_currentEngagement.EngagementId);
            }
            catch (Exception ex)
            {
                ShowError($"Failed to delete engagement: {ex.Message}");
                return;
            }

            EnterNewMode();
            RefreshEngagements();
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
                var service = new EngagementService(context);
                if (_isNew)
                {
                    var entity = new DimEngagement
                    {
                        EngagementId = snapshot.Id,
                        EngagementTitle = snapshot.Title,
                        EngagementPartner = snapshot.Partner,
                        EngagementManager = snapshot.Manager,
                        OpeningMargin = snapshot.OpeningMargin,
                        IsActive = true
                    };
                    service.Insert(entity);
                    RefreshEngagements(entity.EngagementId);
                    MessageBox.Show(
                        this,
                        $"Engagement '{entity.EngagementId}' was created successfully.",
                        "Engagement Saved",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                else
                {
                    var entity = new DimEngagement
                    {
                        EngagementId = snapshot.Id,
                        EngagementTitle = snapshot.Title,
                        EngagementPartner = snapshot.Partner,
                        EngagementManager = snapshot.Manager,
                        OpeningMargin = snapshot.OpeningMargin,
                        IsActive = _currentEngagement?.IsActive ?? true
                    };
                    service.Update(entity);
                    RefreshEngagements(entity.EngagementId);
                }
            }
            catch (Exception ex)
            {
                ShowError($"Failed to save engagement: {ex.Message}");
            }
        }

        private bool TryGetEditorSnapshot(out EngagementEditorSnapshot snapshot, out string? errorMessage)
        {
            snapshot = default;
            errorMessage = null;

            var id = txtEngagementId.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(id))
            {
                errorMessage = "Engagement ID is required.";
                return false;
            }

            if (id.Length > 64)
            {
                errorMessage = "Engagement ID must be 64 characters or less.";
                return false;
            }

            var title = txtEngagementTitle.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(title))
            {
                errorMessage = "Engagement name is required.";
                return false;
            }

            if (title.Length > 255)
            {
                errorMessage = "Engagement name must be 255 characters or less.";
                return false;
            }

            var partner = string.IsNullOrWhiteSpace(txtEngagementPartner.Text)
                ? null
                : txtEngagementPartner.Text.Trim();
            if (partner != null && partner.Length > 255)
            {
                errorMessage = "Engagement partner must be 255 characters or less.";
                return false;
            }

            var manager = string.IsNullOrWhiteSpace(txtEngagementManager.Text)
                ? null
                : txtEngagementManager.Text.Trim();
            if (manager != null && manager.Length > 255)
            {
                errorMessage = "Engagement manager must be 255 characters or less.";
                return false;
            }

            var marginText = txtOpeningMargin.Text?.Trim() ?? string.Empty;
            if (!decimal.TryParse(marginText, NumberStyles.Number, CultureInfo.InvariantCulture, out var openingMargin) &&
                !decimal.TryParse(marginText, NumberStyles.Number, CultureInfo.CurrentCulture, out openingMargin))
            {
                errorMessage = "Opening margin must be a valid decimal number.";
                return false;
            }

            openingMargin = Math.Round(openingMargin, 3, MidpointRounding.AwayFromZero);
            if (openingMargin < -100m || openingMargin > 100m)
            {
                errorMessage = "Opening margin must be between -100.000 and 100.000.";
                return false;
            }

            snapshot = new EngagementEditorSnapshot(id, title, partner, manager, openingMargin);
            return true;
        }

        private void Editor_TextChanged(object sender, EventArgs e)
        {
            if (_suppressEvents)
            {
                return;
            }

            UpdateButtonStates();
        }

        private void UpdateButtonStates()
        {
            if (_suppressEvents)
            {
                return;
            }

            btnDelete.Enabled = !_isNew && _currentEngagement != null;

            if (TryGetEditorSnapshot(out var snapshot, out _))
            {
                btnSave.Enabled = _isNew || _currentEngagement == null || IsDirty(snapshot);
            }
            else
            {
                btnSave.Enabled = false;
            }
        }

        private bool IsDirty(in EngagementEditorSnapshot snapshot)
        {
            if (_currentEngagement == null)
            {
                return true;
            }

            if (!string.Equals(_currentEngagement.EngagementTitle, snapshot.Title, StringComparison.Ordinal))
            {
                return true;
            }

            if (!StringEquals(_currentEngagement.EngagementPartner, snapshot.Partner))
            {
                return true;
            }

            if (!StringEquals(_currentEngagement.EngagementManager, snapshot.Manager))
            {
                return true;
            }

            var currentMargin = Math.Round(_currentEngagement.OpeningMargin, 3, MidpointRounding.AwayFromZero);
            return currentMargin != snapshot.OpeningMargin;
        }

        private static bool StringEquals(string? a, string? b)
        {
            var left = string.IsNullOrWhiteSpace(a) ? string.Empty : a.Trim();
            var right = string.IsNullOrWhiteSpace(b) ? string.Empty : b.Trim();
            return string.Equals(left, right, StringComparison.Ordinal);
        }

        private void ShowError(string message)
        {
            MessageBox.Show(this, message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private readonly struct EngagementEditorSnapshot
        {
            public EngagementEditorSnapshot(string id, string title, string? partner, string? manager, decimal openingMargin)
            {
                Id = id;
                Title = title;
                Partner = partner;
                Manager = manager;
                OpeningMargin = openingMargin;
            }

            public string Id { get; }
            public string Title { get; }
            public string? Partner { get; }
            public string? Manager { get; }
            public decimal OpeningMargin { get; }
        }
    }
}
