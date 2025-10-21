using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;
using GRCFinancialControl.Avalonia.Services.Interfaces;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public sealed partial class HoursAllocationsViewModel : ViewModelBase
    {
        private readonly IEngagementService _engagementService;
        private readonly IHoursAllocationService _hoursAllocationService;
        private readonly ILoggingService _loggingService;

        private int? _lastSelectedEngagementId;
        private bool _suppressSelectionChanged;

        [ObservableProperty]
        private ObservableCollection<EngagementSummaryViewModel> _engagements = new();

        [ObservableProperty]
        private EngagementSummaryViewModel? _selectedEngagement;

        [ObservableProperty]
        private ObservableCollection<FiscalYearAllocationInfo> _fiscalYears = new();

        [ObservableProperty]
        private ObservableCollection<HoursAllocationRowViewModel> _rows = new();

        [ObservableProperty]
        private HoursAllocationRowViewModel? _selectedRow;

        [ObservableProperty]
        private decimal _totalBudgetHours;

        [ObservableProperty]
        private decimal _actualHours;

        [ObservableProperty]
        private decimal _toBeConsumedHours;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private bool _hasChanges;

        [ObservableProperty]
        private string? _statusMessage;

        [ObservableProperty]
        private string _newRankName = string.Empty;

        public HoursAllocationsViewModel(
            IEngagementService engagementService,
            IHoursAllocationService hoursAllocationService,
            ILoggingService loggingService,
            IMessenger messenger)
            : base(messenger)
        {
            _engagementService = engagementService ?? throw new ArgumentNullException(nameof(engagementService));
            _hoursAllocationService = hoursAllocationService ?? throw new ArgumentNullException(nameof(hoursAllocationService));
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        }

        public string Header => "Hours Allocation";

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
                IsBusy = true;
                StatusMessage = null;
                var snapshot = await _hoursAllocationService.SaveAsync(SelectedEngagement.Id, updates).ConfigureAwait(false);
                ApplySnapshot(snapshot);
                StatusMessage = "Changes saved successfully.";
                Messenger.Send(new RefreshDataMessage());
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
                IsBusy = true;
                StatusMessage = null;
                var snapshot = await _hoursAllocationService.AddRankAsync(SelectedEngagement.Id, rankName).ConfigureAwait(false);
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
                IsBusy = true;
                StatusMessage = null;
                await _hoursAllocationService.DeleteRankAsync(SelectedEngagement.Id, SelectedRow.RankName).ConfigureAwait(false);
                var snapshot = await _hoursAllocationService.GetAllocationAsync(SelectedEngagement.Id).ConfigureAwait(false);
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

        partial void OnSelectedEngagementChanged(EngagementSummaryViewModel? value)
        {
            if (_suppressSelectionChanged)
            {
                return;
            }

            _ = LoadSelectedEngagementAsync(value);
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
            var snapshot = await _hoursAllocationService.GetAllocationAsync(engagementId).ConfigureAwait(false);
            ApplySnapshot(snapshot);
        }

        private void ApplySnapshot(HoursAllocationSnapshot snapshot)
        {
            _lastSelectedEngagementId = snapshot.EngagementId;

            ActualHours = snapshot.ActualHours;
            TotalBudgetHours = snapshot.TotalBudgetHours;
            FiscalYears = new ObservableCollection<FiscalYearAllocationInfo>(snapshot.FiscalYears);

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
        }

        private void OnCellChanged()
        {
            RecalculateDirtyState();
        }

        private void RecalculateDirtyState()
        {
            HasChanges = Rows.SelectMany(row => row.Cells).Any(cell => cell.HasChanges);
            RecalculateToBeConsumed();
        }

        private void RecalculateToBeConsumed()
        {
            if (FiscalYears.Count == 0)
            {
                ToBeConsumedHours = ActualHours;
                return;
            }

            var openFiscalYearIds = new HashSet<int>(FiscalYears.Where(fy => !fy.IsLocked).Select(fy => fy.Id));
            var consumed = Rows
                .SelectMany(row => row.Cells)
                .Where(cell => openFiscalYearIds.Contains(cell.FiscalYearId))
                .Sum(cell => cell.ConsumedHours);

            ToBeConsumedHours = ActualHours - consumed;
        }

        private static string NormalizeRank(string value)
        {
            return value?.Trim() ?? string.Empty;
        }

        public sealed partial class HoursAllocationRowViewModel : ObservableObject
        {
            public HoursAllocationRowViewModel(HoursAllocationsViewModel owner, HoursAllocationRowSnapshot snapshot)
            {
                ArgumentNullException.ThrowIfNull(owner);
                ArgumentNullException.ThrowIfNull(snapshot);

                RankName = snapshot.RankName;
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
                    };
                }
            }

            public string RankName { get; }

            public ObservableCollection<HoursAllocationCellViewModel> Cells { get; }

            public bool CanDelete => Cells.All(cell => cell.IsZero);

            public bool HasChanges => Cells.Any(cell => cell.HasChanges);
        }

        public sealed partial class HoursAllocationCellViewModel : ObservableObject
        {
            private readonly Action _notifyChange;
            private readonly decimal _originalConsumedHours;

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

            public long? BudgetId { get; }

            public int FiscalYearId { get; }

            public bool IsLocked { get; }

            [ObservableProperty]
            private decimal _budgetHours;

            [ObservableProperty]
            private decimal _consumedHours;

            [ObservableProperty]
            private decimal _remainingHours;

            public bool IsEditable => !IsLocked && BudgetId.HasValue;

            public bool HasChanges => BudgetId.HasValue &&
                Math.Round(ConsumedHours, 2, MidpointRounding.AwayFromZero) !=
                Math.Round(_originalConsumedHours, 2, MidpointRounding.AwayFromZero);

            public bool IsZero =>
                Math.Round(BudgetHours, 2, MidpointRounding.AwayFromZero) == 0m &&
                Math.Round(ConsumedHours, 2, MidpointRounding.AwayFromZero) == 0m;

            partial void OnConsumedHoursChanged(decimal value)
            {
                RemainingHours = BudgetHours - value;
                OnPropertyChanged(nameof(HasChanges));
                OnPropertyChanged(nameof(IsZero));
                _notifyChange();
            }

            partial void OnBudgetHoursChanged(decimal value)
            {
                OnPropertyChanged(nameof(IsZero));
            }
        }

        public sealed record EngagementSummaryViewModel(int Id, string Code, string Name)
        {
            public override string ToString() => string.IsNullOrWhiteSpace(Name) ? Code : $"{Code} - {Name}";
        }

        partial void OnHasChangesChanged(bool value)
        {
            SaveCommand.NotifyCanExecuteChanged();
        }

        partial void OnIsBusyChanged(bool value)
        {
            SaveCommand.NotifyCanExecuteChanged();
            RefreshCommand.NotifyCanExecuteChanged();
            AddRankCommand.NotifyCanExecuteChanged();
            DeleteRankCommand.NotifyCanExecuteChanged();
        }
    }
}
