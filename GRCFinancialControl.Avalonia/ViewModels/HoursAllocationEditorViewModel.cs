using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;
using GRCFinancialControl.Avalonia.Services.Interfaces;
using GRCFinancialControl.Core.Enums;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public sealed partial class HoursAllocationEditorViewModel : ViewModelBase
    {
        private readonly IHoursAllocationService _hoursAllocationService;
        private readonly ILoggingService _loggingService;
        private readonly int _engagementId;

        [ObservableProperty]
        private string _engagementCode = string.Empty;

        [ObservableProperty]
        private string _engagementName = string.Empty;

        [ObservableProperty]
        private ObservableCollection<FiscalYearAllocationInfo> _fiscalYears = new();

        [ObservableProperty]
        private ObservableCollection<RankOption> _rankOptions = new();

        [ObservableProperty]
        private ObservableCollection<HoursAllocationEditorRowViewModel> _rows = new();

        [ObservableProperty]
        private HoursAllocationEditorRowViewModel? _selectedRow;

        [ObservableProperty]
        private RankOption? _rankToAdd;

        [ObservableProperty]
        private decimal _totalBudgetHours;

        [ObservableProperty]
        private decimal _actualHours;

        [ObservableProperty]
        private decimal _remainingHours;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private bool _hasChanges;

        [ObservableProperty]
        private string? _statusMessage;

        public HoursAllocationEditorViewModel(
            HoursAllocationSnapshot snapshot,
            IHoursAllocationService hoursAllocationService,
            ILoggingService loggingService,
            IMessenger messenger,
            string? initialRank = null)
            : base(messenger)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            _hoursAllocationService = hoursAllocationService ?? throw new ArgumentNullException(nameof(hoursAllocationService));
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));

            _engagementId = snapshot.EngagementId;

            InitialRank = initialRank;

            ApplySnapshot(snapshot);
        }

        public string Header => $"{EngagementCode} · {EngagementName}";

        public string? InitialRank { get; }

        [RelayCommand]
        private async Task AddResourceAsync()
        {
            if (RankToAdd is null || string.IsNullOrWhiteSpace(RankToAdd.Name))
            {
                StatusMessage = "Select a rank before adding.";
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
                var snapshot = await _hoursAllocationService.AddRankAsync(_engagementId, RankToAdd.Name).ConfigureAwait(false);
                RankToAdd = null;
                ApplySnapshot(snapshot);
                StatusMessage = "Rank added to the allocation.";
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
            if (!CanSave())
            {
                return;
            }

            var updates = Rows
                .SelectMany(row => row.CellsByFiscalYear.Values)
                .Where(cell => cell.HasChanges && cell.BudgetId.HasValue)
                .Select(cell => new HoursAllocationCellUpdate(cell.BudgetId!.Value, cell.ConsumedHours))
                .ToList();

            var rowAdjustments = Rows
                .Where(row => row.HasAdditionalChanges)
                .Select(row => new HoursAllocationRowAdjustment(row.RankName, row.AdditionalHours))
                .ToList();

            if (updates.Count == 0 && rowAdjustments.Count == 0)
            {
                StatusMessage = "No changes to save.";
                return;
            }

            try
            {
                IsBusy = true;
                StatusMessage = null;
                var snapshot = await _hoursAllocationService
                    .SaveAsync(_engagementId, updates, rowAdjustments)
                    .ConfigureAwait(false);
                ApplySnapshot(snapshot);
                StatusMessage = "Changes saved successfully.";
                Messenger.Send(new CloseDialogMessage(true));
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
        private void Close()
        {
            Messenger.Send(new CloseDialogMessage(false));
        }

        private bool CanSave() => HasChanges && !IsBusy;

        internal void NotifyRowChanged()
        {
            HasChanges = Rows.Any(row => row.HasChanges);
            RemainingHours = Math.Round(Rows.Sum(row => row.TotalRemainingHours), 2, MidpointRounding.AwayFromZero);
        }

        internal RankOption? FindRankOption(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            return RankOptions.FirstOrDefault(option => string.Equals(option.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        internal string GetFiscalYearName(int fiscalYearId)
        {
            var match = FiscalYears.FirstOrDefault(fy => fy.Id == fiscalYearId);
            return match?.Name ?? $"FY {fiscalYearId}";
        }

        private void ApplySnapshot(HoursAllocationSnapshot snapshot)
        {
            EngagementCode = snapshot.EngagementCode;
            EngagementName = snapshot.EngagementName;
            TotalBudgetHours = snapshot.TotalBudgetHours;
            ActualHours = snapshot.ActualHours;

            FiscalYears = new ObservableCollection<FiscalYearAllocationInfo>(snapshot.FiscalYears);
            RankOptions = new ObservableCollection<RankOption>(snapshot.RankOptions);

            var rows = snapshot.Rows
                .Select(row => new HoursAllocationEditorRowViewModel(this, row))
                .ToList();

            Rows = new ObservableCollection<HoursAllocationEditorRowViewModel>(rows);

            foreach (var row in Rows)
            {
                row.PropertyChanged += HandleRowChanged;
            }

            RemainingHours = Math.Round(Rows.Sum(row => row.TotalRemainingHours), 2, MidpointRounding.AwayFromZero);
            HasChanges = Rows.Any(row => row.HasChanges);
            StatusMessage = null;

            if (!string.IsNullOrWhiteSpace(InitialRank))
            {
                SelectedRow = Rows.FirstOrDefault(row => string.Equals(row.RankName, InitialRank, StringComparison.OrdinalIgnoreCase));
            }
        }

        private void HandleRowChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(HoursAllocationEditorRowViewModel.TotalRemainingHours)
                or nameof(HoursAllocationEditorRowViewModel.HasChanges))
            {
                NotifyRowChanged();
            }
        }

        public sealed partial class HoursAllocationEditorRowViewModel : ObservableObject
        {
            private readonly HoursAllocationEditorViewModel _owner;
            private readonly Dictionary<int, HoursAllocationEditorCellViewModel> _cellsByFiscalYear;
            private readonly decimal _originalAdditionalHours;

            public HoursAllocationEditorRowViewModel(
                HoursAllocationEditorViewModel owner,
                HoursAllocationRowSnapshot snapshot)
            {
                _owner = owner ?? throw new ArgumentNullException(nameof(owner));
                ArgumentNullException.ThrowIfNull(snapshot);

                RankName = snapshot.RankName;
                _additionalHours = snapshot.AdditionalHours;
                _originalAdditionalHours = snapshot.AdditionalHours;
                Status = snapshot.Status;
                _incurredHours = snapshot.IncurredHours;

                var cells = snapshot.Cells
                    .Select(cell => new HoursAllocationEditorCellViewModel(cell, owner.GetFiscalYearName(cell.FiscalYearId), NotifyOwner))
                    .ToList();

                Cells = new ObservableCollection<HoursAllocationEditorCellViewModel>(cells);
                _cellsByFiscalYear = cells.ToDictionary(cell => cell.FiscalYearId);

                foreach (var cell in Cells)
                {
                    cell.PropertyChanged += (_, args) =>
                    {
                        if (args.PropertyName is nameof(HoursAllocationEditorCellViewModel.ConsumedHours)
                            or nameof(HoursAllocationEditorCellViewModel.RemainingHours))
                        {
                            OnPropertyChanged(nameof(TotalConsumedHours));
                            OnPropertyChanged(nameof(TotalRemainingHours));
                        }

                        if (args.PropertyName is nameof(HoursAllocationEditorCellViewModel.HasChanges))
                        {
                            OnPropertyChanged(nameof(HasChanges));
                        }

                        _owner.NotifyRowChanged();
                    };
                }

                UpdateStatus();
            }

            public string RankName { get; }

            public ObservableCollection<HoursAllocationEditorCellViewModel> Cells { get; }

            public IReadOnlyDictionary<int, HoursAllocationEditorCellViewModel> CellsByFiscalYear => _cellsByFiscalYear;

            public RankOption? DisplayRank => _owner.FindRankOption(RankName);

            [ObservableProperty]
            private decimal _additionalHours;

            [ObservableProperty]
            private decimal _incurredHours;

            [ObservableProperty]
            private TrafficLightStatus _status;

            public decimal TotalBudgetHours => Math.Round(Cells.Sum(cell => cell.BudgetHours), 2, MidpointRounding.AwayFromZero);

            public decimal TotalConsumedHours => Math.Round(Cells.Sum(cell => cell.ConsumedHours), 2, MidpointRounding.AwayFromZero);

            public decimal TotalRemainingHours => Math.Round(Cells.Sum(cell => cell.RemainingHours) + AdditionalHours, 2, MidpointRounding.AwayFromZero);

            public bool HasChanges => Cells.Any(cell => cell.HasChanges) || HasAdditionalChanges;

            public bool HasAdditionalChanges =>
                Math.Round(AdditionalHours, 2, MidpointRounding.AwayFromZero) !=
                Math.Round(_originalAdditionalHours, 2, MidpointRounding.AwayFromZero);

            public string TrafficLightSymbol => Status switch
            {
                TrafficLightStatus.Red => "🔴",
                TrafficLightStatus.Yellow => "🟡",
                _ => "🟢"
            };

            partial void OnAdditionalHoursChanged(decimal value)
            {
                var normalized = Math.Round(value, 2, MidpointRounding.AwayFromZero);
                if (normalized != value)
                {
                    AdditionalHours = normalized;
                    return;
                }

                OnPropertyChanged(nameof(TotalRemainingHours));
                OnPropertyChanged(nameof(HasChanges));
                OnPropertyChanged(nameof(HasAdditionalChanges));
                UpdateStatus();
                _owner.NotifyRowChanged();
            }

            partial void OnStatusChanged(TrafficLightStatus value)
            {
                OnPropertyChanged(nameof(TrafficLightSymbol));
            }

            private void NotifyOwner()
            {
                OnPropertyChanged(nameof(TotalConsumedHours));
                OnPropertyChanged(nameof(TotalRemainingHours));
                OnPropertyChanged(nameof(HasChanges));
                UpdateStatus();
                _owner.NotifyRowChanged();
            }

            private void UpdateStatus()
            {
                var remaining = TotalRemainingHours;
                var status = remaining < 0m
                    ? TrafficLightStatus.Red
                    : remaining > 0m
                        ? TrafficLightStatus.Yellow
                        : TrafficLightStatus.Green;

                Status = status;
            }
        }

        public sealed partial class HoursAllocationEditorCellViewModel : ObservableObject
        {
            private readonly Action _notifyChange;
            private readonly decimal _originalConsumedHours;

            public HoursAllocationEditorCellViewModel(HoursAllocationCellSnapshot snapshot, string fiscalYearName, Action notifyChange)
            {
                ArgumentNullException.ThrowIfNull(snapshot);
                _notifyChange = notifyChange ?? throw new ArgumentNullException(nameof(notifyChange));

                BudgetId = snapshot.BudgetId;
                FiscalYearId = snapshot.FiscalYearId;
                FiscalYearName = fiscalYearName;
                _budgetHours = snapshot.BudgetHours;
                _consumedHours = snapshot.ConsumedHours;
                _remainingHours = snapshot.RemainingHours;
                IsLocked = snapshot.IsLocked;
                _originalConsumedHours = snapshot.ConsumedHours;
            }

            public long? BudgetId { get; }

            public int FiscalYearId { get; }

            public string FiscalYearName { get; }

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
                _notifyChange();
            }

            partial void OnBudgetHoursChanged(decimal value)
            {
                RemainingHours = Math.Round(value - ConsumedHours, 2, MidpointRounding.AwayFromZero);
            }
        }
    }
}
