using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using App.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRC.Shared.UI.Messages;
using GRCFinancialControl.Avalonia.Messages;
using GRCFinancialControl.Avalonia.Services;
using GRCFinancialControl.Core.Enums;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Importers;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    /// <summary>
    /// Coordinates engagement selection and hours allocation editing workflows.
    /// </summary>
    public sealed partial class HoursAllocationDetailViewModel : ViewModelBase, IRecipient<ApplicationParametersChangedMessage>
    {
        private readonly IEngagementService _engagementService;
        private readonly IHoursAllocationService _hoursAllocationService;
        private readonly IAllocationSnapshotService _allocationSnapshotService;
        private readonly AllocationPlanningImporter _allocationImporter;
        private readonly FilePickerService _filePickerService;
        private readonly LoggingService _loggingService;
        private readonly DialogService _dialogService;
        private readonly IClosingPeriodService _closingPeriodService;
        private readonly ISettingsService _settingsService;

        private int? _lastSelectedEngagementId;
        private bool _suppressSelectionChanged;
        private HoursAllocationSnapshot? _currentSnapshot;
        private int? _pendingClosingPeriodId;

        /// <summary>
        /// Gets or sets the engagements that can be viewed or edited.
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<EngagementSummaryViewModel> _engagements = new();

        /// <summary>
        /// Gets or sets the engagement currently selected by the user.
        /// </summary>
        [ObservableProperty]
        private EngagementSummaryViewModel? _selectedEngagement;

        /// <summary>
        /// Gets or sets the fiscal year allocation metadata for the selected engagement.
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<FiscalYearAllocationInfo> _fiscalYears = new();

        /// <summary>
        /// Gets or sets the editable rows displayed to the user.
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<HoursAllocationRowViewModel> _rows = new();

        /// <summary>
        /// Gets or sets the available rank options that can be assigned to resources.
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<RankOption> _availableRanks = new();

        /// <summary>
        /// Gets or sets the closing periods available for selection.
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<ClosingPeriod> _closingPeriods = new();

        /// <summary>
        /// Gets or sets the closing period currently selected by the user.
        /// </summary>
        [ObservableProperty]
        private ClosingPeriod? _selectedClosingPeriod;

        /// <summary>
        /// Gets or sets the allocation row currently focused in the grid.
        /// </summary>
        [ObservableProperty]
        private HoursAllocationRowViewModel? _selectedRow;

        /// <summary>
        /// Gets or sets the rank option currently selected for new resources.
        /// </summary>
        [ObservableProperty]
        private RankOption? _selectedRankOption;

        /// <summary>
        /// Gets or sets the total budgeted hours for the selected engagement.
        /// </summary>
        [ObservableProperty]
        private decimal _totalBudgetHours;

        /// <summary>
        /// Gets or sets the total actual hours consumed.
        /// </summary>
        [ObservableProperty]
        private decimal _actualHours;

        /// <summary>
        /// Gets or sets the total hours still to be consumed.
        /// </summary>
        [ObservableProperty]
        private decimal _toBeConsumedHours;

        /// <summary>
        /// Gets or sets a value indicating whether the view model is performing background work.
        /// </summary>
        [ObservableProperty]
        private bool _isBusy;

        /// <summary>
        /// Gets or sets a value indicating whether the current snapshot contains unsaved changes.
        /// </summary>
        [ObservableProperty]
        private bool _hasChanges;

        /// <summary>
        /// Gets or sets the informational status message presented to the user.
        /// </summary>
        [ObservableProperty]
        private string? _statusMessage;

        /// <summary>
        /// Gets or sets the name for a new rank when created through the UI.
        /// </summary>
        [ObservableProperty]
        private string _newRankName = string.Empty;

        /// <summary>
        /// Gets or sets the discrepancy report from allocation vs imported values.
        /// </summary>
        [ObservableProperty]
        private AllocationDiscrepancyReport? _discrepancies;

        /// <summary>
        /// Gets a value indicating whether discrepancies exist between allocations and imported values.
        /// </summary>
        public bool HasDiscrepancies => Discrepancies?.HasDiscrepancies ?? false;

        /// <summary>
        /// Initializes a new instance of the <see cref="HoursAllocationDetailViewModel"/> class.
        /// </summary>
        /// <param name="engagementService">Provides engagement retrieval operations.</param>
        /// <param name="hoursAllocationService">Persists hours allocation edits.</param>
        /// <param name="allocationSnapshotService">Manages allocation snapshots per closing period.</param>
        /// <param name="allocationImporter">Handles allocation planning and history imports.</param>
        /// <param name="filePickerService">Supplies file picking dialogs.</param>
        /// <param name="loggingService">Records error and information logs.</param>
        /// <param name="dialogService">Shows user confirmation dialogs.</param>
        /// <param name="closingPeriodService">Resolves closing periods for fiscal years.</param>
        /// <param name="settingsService">Accesses persisted user settings.</param>
        /// <param name="messenger">Coordinates notifications across view models.</param>
        public HoursAllocationDetailViewModel(
            IEngagementService engagementService,
            IHoursAllocationService hoursAllocationService,
            IAllocationSnapshotService allocationSnapshotService,
            AllocationPlanningImporter allocationImporter,
            FilePickerService filePickerService,
            LoggingService loggingService,
            DialogService dialogService,
            IClosingPeriodService closingPeriodService,
            ISettingsService settingsService,
            IMessenger messenger)
            : base(messenger)
        {
            _engagementService = engagementService ?? throw new ArgumentNullException(nameof(engagementService));
            _hoursAllocationService = hoursAllocationService ?? throw new ArgumentNullException(nameof(hoursAllocationService));
            _allocationSnapshotService = allocationSnapshotService ?? throw new ArgumentNullException(nameof(allocationSnapshotService));
            _allocationImporter = allocationImporter ?? throw new ArgumentNullException(nameof(allocationImporter));
            _filePickerService = filePickerService ?? throw new ArgumentNullException(nameof(filePickerService));
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _closingPeriodService = closingPeriodService ?? throw new ArgumentNullException(nameof(closingPeriodService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        }

        /// <summary>
        /// Gets the header text shown in the hours allocation view.
        /// </summary>
        public string Header => "Hours Allocation";

        /// <summary>
        /// Sets the engagement that should be selected when loading data.
        /// </summary>
        /// <param name="engagementId">The engagement identifier to preselect.</param>
        public void InitializeSelection(int engagementId)
        {
            _lastSelectedEngagementId = engagementId;

            if (Engagements.Count == 0)
            {
                return;
            }

            var match = Engagements.FirstOrDefault(e => e.Id == engagementId);
            if (match is not null)
            {
                _suppressSelectionChanged = true;
                SelectedEngagement = match;
                _suppressSelectionChanged = false;
            }
        }

        /// <inheritdoc />
        public override async Task LoadDataAsync()
        {
            if (IsBusy)
            {
                return;
            }

            try
            {
                IsBusy = true;
                StatusMessage = null;

                await LoadClosingPeriodsAsync().ConfigureAwait(false);

                var engagements = await _engagementService.GetAllAsync().ConfigureAwait(false);
                var summaries = engagements
                    .OrderBy(e => e.EngagementId, StringComparer.OrdinalIgnoreCase)
                    .Select(e => new EngagementSummaryViewModel(e.Id, e.EngagementId, e.Description))
                    .ToList();

                Engagements = new ObservableCollection<EngagementSummaryViewModel>(summaries);

                EngagementSummaryViewModel? nextSelection = null;
                if (_lastSelectedEngagementId.HasValue)
                {
                    nextSelection = summaries.FirstOrDefault(e => e.Id == _lastSelectedEngagementId.Value);
                }

                nextSelection ??= summaries.FirstOrDefault();

                _suppressSelectionChanged = true;
                SelectedEngagement = nextSelection;
                _suppressSelectionChanged = false;

                if (nextSelection is not null)
                {
                    await LoadSnapshotAsync(nextSelection.Id).ConfigureAwait(false);
                }
                else
                {
                    ClearSnapshot();
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex.Message);
                StatusMessage = ex.Message;
                ClearSnapshot();
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Reloads data for the currently selected engagement.
        /// </summary>
        [RelayCommand]
        private async Task RefreshAsync()
        {
            if (SelectedEngagement is null || IsBusy)
            {
                return;
            }

            try
            {
                IsBusy = true;
                StatusMessage = null;
                await LoadSnapshotAsync(SelectedEngagement.Id).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex.Message);
                StatusMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Adds a new resource entry using the provided or selected rank option.
        /// </summary>
        [RelayCommand]
        private async Task AddResourceAsync(RankOption? rankOption)
        {
            if (!CanAddResource(rankOption))
            {
                return;
            }

            rankOption ??= SelectedRankOption ?? AvailableRanks.FirstOrDefault();
            if (SelectedEngagement is null || rankOption is null)
            {
                return;
            }

            SelectedRankOption = rankOption;

            var addedRankCode = rankOption.Code;
            var addedRankName = rankOption.DisplayName;

            try
            {
                IsBusy = true;
                StatusMessage = null;

                if (SelectedClosingPeriod == null)
                {
                    StatusMessage = "Please select a closing period first.";
                    return;
                }

                var snapshot = await _hoursAllocationService
                    .AddRankAsync(SelectedEngagement.Id, SelectedClosingPeriod.Id, rankOption.Code)
                    .ConfigureAwait(false);

                ApplySnapshot(snapshot);

                SelectedRow = Rows.FirstOrDefault(row =>
                    string.Equals(row.RankName, addedRankCode, StringComparison.OrdinalIgnoreCase));

                StatusMessage = $"Rank '{addedRankName}' added to the allocation.";
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex.Message);
                StatusMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool CanAddResource(RankOption? option = null)
        {
            option ??= SelectedRankOption;
            return !IsBusy &&
                   SelectedEngagement is not null &&
                   option is not null &&
                   !string.IsNullOrWhiteSpace(option.Code);
        }

        private async Task LoadClosingPeriodsAsync()
        {
            var periods = await _closingPeriodService.GetAllAsync().ConfigureAwait(false);
            var ordered = periods
                .OrderByDescending(period => period.PeriodStart)
                .ToList();

            ClosingPeriods = new ObservableCollection<ClosingPeriod>(ordered);
            var preferredClosingPeriodId = _pendingClosingPeriodId
                ?? await _settingsService.GetDefaultClosingPeriodIdAsync().ConfigureAwait(false);
            _pendingClosingPeriodId = null;

            if (preferredClosingPeriodId.HasValue)
            {
                var matched = ordered.FirstOrDefault(p => p.Id == preferredClosingPeriodId.Value);
                SelectedClosingPeriod = matched ?? ordered.FirstOrDefault();
            }
            else if (SelectedClosingPeriod is not null)
            {
                SelectedClosingPeriod = ordered.FirstOrDefault(p => p.Id == SelectedClosingPeriod.Id)
                    ?? ordered.FirstOrDefault();
            }
            else
            {
                SelectedClosingPeriod = ordered.FirstOrDefault();
            }
        }

        /// <summary>
        /// Handles updates to the application parameters by reconciling the selected closing period.
        /// </summary>
        /// <param name="message">The message containing updated parameter values.</param>
        public void Receive(ApplicationParametersChangedMessage message)
        {
            if (message is null)
            {
                return;
            }

            _pendingClosingPeriodId = message.ClosingPeriodId;

            if (message.ClosingPeriodId is null)
            {
                SelectedClosingPeriod = ClosingPeriods.FirstOrDefault();
                return;
            }

            var match = ClosingPeriods.FirstOrDefault(period => period.Id == message.ClosingPeriodId.Value);
            if (match != null)
            {
                SelectedClosingPeriod = match;
            }
            else
            {
                _ = LoadClosingPeriodsAsync();
            }
        }

        /// <summary>
        /// Copies hours allocation from the previous closing period to the currently selected period.
        /// </summary>
        [RelayCommand]
        private async Task CopyFromPreviousPeriodAsync()
        {
            if (SelectedEngagement is null || SelectedClosingPeriod is null || IsBusy)
            {
                return;
            }

            try
            {
                IsBusy = true;
                StatusMessage = null;

                var copiedBudgets = await _allocationSnapshotService
                    .CreateHoursSnapshotFromPreviousPeriodAsync(
                        SelectedEngagement.Id,
                        SelectedClosingPeriod.Id)
                    .ConfigureAwait(false);

                // Reload allocation with copied data
                await LoadSnapshotAsync(SelectedEngagement.Id).ConfigureAwait(false);

                StatusMessage = copiedBudgets.Count > 0
                    ? $"Copied {copiedBudgets.Count} allocations from previous period."
                    : "No previous period allocations found to copy.";
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex.Message);
                StatusMessage = $"Error copying from previous period: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Imports the latest allocation workbook for the selected closing period.
        /// </summary>
        [RelayCommand]
        private async Task UpdateAllocationsAsync()
        {
            if (IsBusy)
            {
                return;
            }

            if (SelectedClosingPeriod is null)
            {
                StatusMessage = "Please select a Closing Period before importing the allocation sheet.";
                return;
            }

            var filePath = await _filePickerService.OpenFileAsync(
                title: "Select staff allocation workbook",
                defaultExtension: ".xlsx",
                allowedPatterns: new[] { "*.xlsx" });

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            var selectedId = SelectedEngagement?.Id;
            var updateCompleted = false;

            try
            {
                IsBusy = true;
                StatusMessage = null;

                var closingPeriodId = SelectedClosingPeriod.Id;
                var summary = await Task.Run(() => _allocationImporter.UpdateHistoryAsync(filePath, closingPeriodId)).ConfigureAwait(false);

                if (selectedId.HasValue)
                {
                    await LoadSnapshotAsync(selectedId.Value).ConfigureAwait(false);
                }

                _loggingService.LogInfo(summary);
                StatusMessage = summary;
                Messenger.Send(new RefreshViewMessage(RefreshTargets.FinancialData));
                updateCompleted = true;
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex.Message);
                StatusMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }

            if (!selectedId.HasValue && updateCompleted)
            {
                await LoadDataAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Persists allocation edits made in the grid.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanSave))]
        private async Task SaveAsync()
        {
            if (SelectedEngagement is null)
            {
                return;
            }

            var updates = Rows
                .SelectMany(row => row.Cells)
                .Where(cell => cell.HasChanges && cell.BudgetId.HasValue)
                .Select(cell => new HoursAllocationCellUpdate(cell.BudgetId!.Value, cell.ConsumedHours))
                .ToList();

            if (updates.Count == 0)
            {
                StatusMessage = "No changes to save.";
                return;
            }

            try
            {
                if (SelectedClosingPeriod == null)
                {
                    StatusMessage = "Please select a closing period first.";
                    return;
                }

                IsBusy = true;
                StatusMessage = null;
                Discrepancies = null;
                var snapshot = await _hoursAllocationService
                    .SaveAsync(SelectedEngagement.Id, SelectedClosingPeriod.Id, updates, Array.Empty<HoursAllocationRowAdjustment>())
                    .ConfigureAwait(false);
                ApplySnapshot(snapshot);

                // Detect discrepancies after save
                var discrepancyReport = await _allocationSnapshotService
                    .DetectDiscrepanciesAsync(SelectedEngagement.Id, SelectedClosingPeriod.Id)
                    .ConfigureAwait(false);

                if (discrepancyReport.HasDiscrepancies)
                {
                    Discrepancies = discrepancyReport;
                    OnPropertyChanged(nameof(HasDiscrepancies));
                    StatusMessage = "Changes saved successfully. Please review discrepancies.";
                }
                else
                {
                    StatusMessage = "Changes saved successfully.";
                }

                Messenger.Send(new RefreshViewMessage(RefreshTargets.FinancialData));
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex.Message);
                StatusMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool CanSave() => HasChanges && !IsBusy && SelectedEngagement is not null;

        /// <summary>
        /// Adds a new rank to the allocation for all open fiscal years.
        /// </summary>
        [RelayCommand]
        private async Task AddRankAsync()
        {
            if (SelectedEngagement is null || IsBusy)
            {
                return;
            }

            var rankName = NormalizeRank(NewRankName);
            if (string.IsNullOrWhiteSpace(rankName))
            {
                StatusMessage = "Provide a rank name before adding.";
                return;
            }

            try
            {
                if (SelectedClosingPeriod == null)
                {
                    StatusMessage = "Please select a closing period first.";
                    return;
                }

                IsBusy = true;
                StatusMessage = null;
                var snapshot = await _hoursAllocationService.AddRankAsync(SelectedEngagement.Id, SelectedClosingPeriod.Id, rankName).ConfigureAwait(false);
                ApplySnapshot(snapshot);
                SelectedRow = Rows.FirstOrDefault(row => string.Equals(row.RankName, rankName, StringComparison.OrdinalIgnoreCase));
                StatusMessage = $"Rank '{rankName}' created for open fiscal years.";
                NewRankName = string.Empty;
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex.Message);
                StatusMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Removes the selected rank when all values are zero.
        /// </summary>
        [RelayCommand]
        private async Task DeleteRankAsync()
        {
            if (SelectedEngagement is null || SelectedRow is null || IsBusy)
            {
                return;
            }

            if (!SelectedRow.CanDelete)
            {
                StatusMessage = "The selected rank cannot be removed because it contains non-zero values.";
                return;
            }

            try
            {
                if (SelectedClosingPeriod == null)
                {
                    StatusMessage = "Please select a closing period first.";
                    return;
                }

                IsBusy = true;
                StatusMessage = null;
                await _hoursAllocationService.DeleteRankAsync(SelectedEngagement.Id, SelectedClosingPeriod.Id, SelectedRow.RankName).ConfigureAwait(false);
                var snapshot = await _hoursAllocationService.GetAllocationAsync(SelectedEngagement.Id, SelectedClosingPeriod.Id).ConfigureAwait(false);
                ApplySnapshot(snapshot);
                SelectedRow = null;
                StatusMessage = "Rank removed.";
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex.Message);
                StatusMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Opens the allocation editor dialog for the provided row.
        /// </summary>
        [RelayCommand]
        private async Task EditRowAsync(HoursAllocationRowViewModel? row)
        {
            if (_currentSnapshot is null || SelectedEngagement is null)
            {
                return;
            }

            var editor = new HoursAllocationEditorViewModel(
                _currentSnapshot,
                _hoursAllocationService,
                _loggingService,
                Messenger,
                row?.RankName);

            var result = await _dialogService.ShowDialogAsync(editor, "Hours Allocation Editor").ConfigureAwait(false);

            if (!result || SelectedEngagement is null)
            {
                return;
            }

            await LoadSnapshotAsync(SelectedEngagement.Id).ConfigureAwait(false);
        }

        partial void OnSelectedEngagementChanged(EngagementSummaryViewModel? value)
        {
            if (_suppressSelectionChanged)
            {
                return;
            }

            _ = LoadSelectedEngagementAsync(value);
            AddResourceCommand.NotifyCanExecuteChanged();
        }

        private async Task LoadSelectedEngagementAsync(EngagementSummaryViewModel? summary)
        {
            if (summary is null)
            {
                ClearSnapshot();
                return;
            }

            if (IsBusy)
            {
                return;
            }

            try
            {
                IsBusy = true;
                StatusMessage = null;
                await LoadSnapshotAsync(summary.Id).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex.Message);
                StatusMessage = ex.Message;
                ClearSnapshot();
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadSnapshotAsync(int engagementId)
        {
            if (SelectedClosingPeriod == null)
            {
                StatusMessage = "Please select a closing period first.";
                return;
            }

            var snapshot = await _hoursAllocationService.GetAllocationAsync(engagementId, SelectedClosingPeriod.Id).ConfigureAwait(false);
            ApplySnapshot(snapshot);
        }

        private void ApplySnapshot(HoursAllocationSnapshot snapshot)
        {
            _lastSelectedEngagementId = snapshot.EngagementId;

            _currentSnapshot = snapshot;

            ActualHours = snapshot.ActualHours;
            TotalBudgetHours = snapshot.TotalBudgetHours;
            FiscalYears = new ObservableCollection<FiscalYearAllocationInfo>(snapshot.FiscalYears);
            AvailableRanks = new ObservableCollection<RankOption>(snapshot.RankOptions);
            SelectedRankOption = AvailableRanks.FirstOrDefault();

            var rows = new List<HoursAllocationRowViewModel>(snapshot.Rows.Count);
            foreach (var row in snapshot.Rows)
            {
                rows.Add(new HoursAllocationRowViewModel(this, row));
            }

            Rows = new ObservableCollection<HoursAllocationRowViewModel>(rows);

            RecalculateDirtyState();
            ToBeConsumedHours = snapshot.ToBeConsumedHours;
            StatusMessage = null;
            SelectedRow = null;
        }

        private void ClearSnapshot()
        {
            Rows = new ObservableCollection<HoursAllocationRowViewModel>();
            FiscalYears = new ObservableCollection<FiscalYearAllocationInfo>();
            TotalBudgetHours = 0m;
            ActualHours = 0m;
            ToBeConsumedHours = 0m;
            HasChanges = false;
            SelectedRow = null;
            SelectedRankOption = null;
        }

        private void OnCellChanged()
        {
            RecalculateDirtyState();
        }

        private void RecalculateDirtyState()
        {
            var cells = Rows.SelectMany(row => row.Cells).ToList();
            HasChanges = cells.Any(cell => cell.HasChanges);
            RecalculateToBeConsumed(cells);
        }

        private void RecalculateToBeConsumed(IReadOnlyList<HoursAllocationCellViewModel> cells)
        {
            if (FiscalYears.Count == 0 || cells.Count == 0)
            {
                ToBeConsumedHours = ActualHours;
                return;
            }

            var openFiscalYearIds = new HashSet<int>(FiscalYears.Where(fy => !fy.IsLocked).Select(fy => fy.Id));
            decimal consumed = 0m;

            foreach (var cell in cells)
            {
                if (openFiscalYearIds.Contains(cell.FiscalYearId))
                {
                    consumed += cell.ConsumedHours;
                }
            }

            ToBeConsumedHours = ActualHours - consumed;
        }

        private static string NormalizeRank(string value)
        {
            return value?.Trim() ?? string.Empty;
        }

        /// <summary>
        /// Represents a row of hours allocations for a specific rank.
        /// </summary>
        public sealed partial class HoursAllocationRowViewModel : ObservableObject
        {
            private readonly decimal _additionalHours;
            private readonly TrafficLightStatus _status;

            /// <summary>
            /// Initializes a new instance of the <see cref="HoursAllocationRowViewModel"/> class.
            /// </summary>
            /// <param name="owner">The owning view model coordinating updates.</param>
            /// <param name="snapshot">The snapshot describing the persisted values.</param>
            public HoursAllocationRowViewModel(HoursAllocationDetailViewModel owner, HoursAllocationRowSnapshot snapshot)
            {
                ArgumentNullException.ThrowIfNull(owner);
                ArgumentNullException.ThrowIfNull(snapshot);

                RankName = snapshot.RankName;
                _additionalHours = snapshot.AdditionalHours;
                _status = snapshot.Status;
                var cells = snapshot.Cells
                    .Select(cell => new HoursAllocationCellViewModel(cell, owner.OnCellChanged))
                    .ToList();

                Cells = new ObservableCollection<HoursAllocationCellViewModel>(cells);

                foreach (var cell in Cells)
                {
                    cell.PropertyChanged += (_, args) =>
                    {
                        if (args.PropertyName is nameof(HoursAllocationCellViewModel.IsZero)
                            or nameof(HoursAllocationCellViewModel.HasChanges))
                        {
                            OnPropertyChanged(nameof(CanDelete));
                            OnPropertyChanged(nameof(HasChanges));
                        }

                        if (args.PropertyName is nameof(HoursAllocationCellViewModel.BudgetHours)
                            or nameof(HoursAllocationCellViewModel.ConsumedHours)
                            or nameof(HoursAllocationCellViewModel.RemainingHours))
                        {
                            OnPropertyChanged(nameof(TotalBudgetHours));
                            OnPropertyChanged(nameof(TotalConsumedHours));
                            OnPropertyChanged(nameof(TotalRemainingHours));
                        }
                    };
                }
            }

            /// <summary>
            /// Gets the rank name for the row.
            /// </summary>
            public string RankName { get; }

            /// <summary>
            /// Gets the collection of cells displayed in the row.
            /// </summary>
            public ObservableCollection<HoursAllocationCellViewModel> Cells { get; }

            /// <summary>
            /// Gets a value indicating whether the row can be deleted without validation errors.
            /// </summary>
            public bool CanDelete => Cells.All(cell => cell.IsZero);

            /// <summary>
            /// Gets a value indicating whether any cell within the row has been modified.
            /// </summary>
            public bool HasChanges => Cells.Any(cell => cell.HasChanges);

            /// <summary>
            /// Gets the sum of budgeted hours for the row.
            /// </summary>
            public decimal TotalBudgetHours => Math.Round(Cells.Sum(cell => cell.BudgetHours), 2, MidpointRounding.AwayFromZero);

            /// <summary>
            /// Gets the sum of consumed hours for the row.
            /// </summary>
            public decimal TotalConsumedHours => Math.Round(Cells.Sum(cell => cell.ConsumedHours), 2, MidpointRounding.AwayFromZero);

            /// <summary>
            /// Gets the remaining hours after accounting for the allocation totals and adjustments.
            /// </summary>
            public decimal TotalRemainingHours => Math.Round(Cells.Sum(cell => cell.RemainingHours) + _additionalHours, 2, MidpointRounding.AwayFromZero);

            /// <summary>
            /// Gets the traffic light status representing the row summary.
            /// </summary>
            public TrafficLightStatus Status => _status;

            /// <summary>
            /// Gets the emoji displayed to represent the traffic light status.
            /// </summary>
            public string TrafficLightSymbol => Status switch
            {
                TrafficLightStatus.Red => "🔴",
                TrafficLightStatus.Yellow => "🟡",
                _ => "🟢"
            };
        }

        /// <summary>
        /// Represents a single hours allocation cell within a fiscal year column.
        /// </summary>
        public sealed partial class HoursAllocationCellViewModel : ObservableObject
        {
            private readonly Action _notifyChange;
            private readonly decimal _originalConsumedHours;

            /// <summary>
            /// Initializes a new instance of the <see cref="HoursAllocationCellViewModel"/> class.
            /// </summary>
            /// <param name="snapshot">The persisted values backing the cell.</param>
            /// <param name="notifyChange">Callback invoked when the cell changes.</param>
            public HoursAllocationCellViewModel(HoursAllocationCellSnapshot snapshot, Action notifyChange)
            {
                ArgumentNullException.ThrowIfNull(snapshot);
                _notifyChange = notifyChange ?? throw new ArgumentNullException(nameof(notifyChange));

                BudgetId = snapshot.BudgetId;
                FiscalYearId = snapshot.FiscalYearId;
                _budgetHours = snapshot.BudgetHours;
                _consumedHours = snapshot.ConsumedHours;
                _remainingHours = snapshot.RemainingHours;
                IsLocked = snapshot.IsLocked;
                _originalConsumedHours = snapshot.ConsumedHours;
            }

            /// <summary>
            /// Gets the identifier of the budget row associated with the cell.
            /// </summary>
            public long? BudgetId { get; }

            /// <summary>
            /// Gets the fiscal year identifier for the cell.
            /// </summary>
            public int FiscalYearId { get; }

            /// <summary>
            /// Gets a value indicating whether the cell is locked from editing.
            /// </summary>
            public bool IsLocked { get; }

            /// <summary>
            /// Gets or sets the budget hours displayed in the cell.
            /// </summary>
            [ObservableProperty]
            private decimal _budgetHours;

            /// <summary>
            /// Gets or sets the consumed hours displayed in the cell.
            /// </summary>
            [ObservableProperty]
            private decimal _consumedHours;

            /// <summary>
            /// Gets or sets the remaining hours displayed in the cell.
            /// </summary>
            [ObservableProperty]
            private decimal _remainingHours;

            /// <summary>
            /// Gets a value indicating whether the cell can be edited by the user.
            /// </summary>
            public bool IsEditable => !IsLocked && BudgetId.HasValue;

            /// <summary>
            /// Gets a value indicating whether the consumed hours differ from the original value.
            /// </summary>
            public bool HasChanges => BudgetId.HasValue &&
                Math.Round(ConsumedHours, 2, MidpointRounding.AwayFromZero) !=
                Math.Round(_originalConsumedHours, 2, MidpointRounding.AwayFromZero);

            /// <summary>
            /// Gets a value indicating whether both budgeted and consumed hours are zero.
            /// </summary>
            public bool IsZero =>
                Math.Round(BudgetHours, 2, MidpointRounding.AwayFromZero) == 0m &&
                Math.Round(ConsumedHours, 2, MidpointRounding.AwayFromZero) == 0m;

            partial void OnConsumedHoursChanged(decimal value)
            {
                var normalized = Math.Round(value, 2, MidpointRounding.AwayFromZero);
                if (normalized != value)
                {
                    ConsumedHours = normalized;
                    return;
                }

                RemainingHours = Math.Round(BudgetHours - value, 2, MidpointRounding.AwayFromZero);
                OnPropertyChanged(nameof(HasChanges));
                OnPropertyChanged(nameof(IsZero));
                _notifyChange();
            }

            partial void OnBudgetHoursChanged(decimal value)
            {
                RemainingHours = Math.Round(value - ConsumedHours, 2, MidpointRounding.AwayFromZero);
                OnPropertyChanged(nameof(IsZero));
            }
        }

        /// <summary>
        /// Represents a lightweight summary of an engagement for selection lists.
        /// </summary>
        public sealed record EngagementSummaryViewModel(int Id, string Code, string Name)
        {
            /// <summary>
            /// Returns the display text for the engagement summary.
            /// </summary>
            /// <returns>The formatted engagement identifier and name.</returns>
            public override string ToString() => string.IsNullOrWhiteSpace(Name) ? Code : $"{Code} - {Name}";
        }

        partial void OnHasChangesChanged(bool value)
        {
            NotifyCommandCanExecute(SaveCommand);
        }

        partial void OnIsBusyChanged(bool value)
        {
            NotifyCommandCanExecute(SaveCommand);
            NotifyCommandCanExecute(RefreshCommand);
            NotifyCommandCanExecute(AddRankCommand);
            NotifyCommandCanExecute(DeleteRankCommand);
            NotifyCommandCanExecute(AddResourceCommand);
        }

        partial void OnSelectedRankOptionChanged(RankOption? value)
        {
            NotifyCommandCanExecute(AddResourceCommand);
        }

        /// <summary>
        /// Closes the dialog without saving changes.
        /// </summary>
        [RelayCommand]
        private void Close()
        {
            Messenger.Send(new CloseDialogMessage(false));
        }
    }
}
