using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;
using GRCFinancialControl.Avalonia.Utilities;
using GRCFinancialControl.Core.Enums;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class EngagementEditorViewModel : ViewModelBase
    {
        private readonly IEngagementService _engagementService;
        private readonly ICustomerService _customerService;
        private readonly IClosingPeriodService _closingPeriodService;
        private readonly IMessenger _messenger;

        [ObservableProperty]
        private string _engagementId = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private string _customerKey = string.Empty;

        [ObservableProperty]
        private Customer? _selectedCustomer;

        [ObservableProperty]
        private ObservableCollection<Customer> _customers = new();

        [ObservableProperty]
        private string _currency = string.Empty;

        [ObservableProperty]
        private decimal _openingMargin;

        [ObservableProperty]
        private decimal _openingValue;

        [ObservableProperty]
        private decimal _openingExpenses;

        [ObservableProperty]
        private EngagementStatus _status;

        [ObservableProperty]
        private double _totalPlannedHours;

        [ObservableProperty]
        private decimal _initialHoursBudget;

        [ObservableProperty]
        private decimal _etcpHours;

        [ObservableProperty]
        private decimal _valueEtcp;

        [ObservableProperty]
        private decimal _expensesEtcp;

        [ObservableProperty]
        private decimal? _marginPctEtcp;

        [ObservableProperty]
        private decimal? _marginPctBudget;

        [ObservableProperty]
        private int? _etcpAgeDays;

        [ObservableProperty]
        private DateTime? _latestEtcDate;

        [ObservableProperty]
        private DateTime? _nextEtcDate;

        public DateTimeOffset? LatestEtcDateOffset
        {
            get => DateTimeOffsetHelper.FromDate(LatestEtcDate);
            set => LatestEtcDate = DateTimeOffsetHelper.ToDate(value);
        }

        public DateTimeOffset? NextEtcDateOffset
        {
            get => DateTimeOffsetHelper.FromDate(NextEtcDate);
            set => NextEtcDate = DateTimeOffsetHelper.ToDate(value);
        }

        [ObservableProperty]
        private string? _statusText;

        [ObservableProperty]
        private string? _lastClosingPeriodId;

        public IEnumerable<EngagementStatus> StatusOptions => Enum.GetValues<EngagementStatus>();

        [ObservableProperty]
        private ObservableCollection<EngagementPapd> _papdAssignments = new();

        [ObservableProperty]
        private ObservableCollection<ClosingPeriod> _closingPeriods = new();

        [ObservableProperty]
        private ObservableCollection<EngagementFinancialEvolutionEntryViewModel> _financialEvolutionEntries = new();

        [ObservableProperty]
        private EngagementFinancialEvolutionEntryViewModel? _selectedFinancialEvolutionEntry;

        public Engagement Engagement { get; }

        public EngagementEditorViewModel(
            Engagement engagement,
            IEngagementService engagementService,
            ICustomerService customerService,
            IClosingPeriodService closingPeriodService,
            IMessenger messenger)
        {
            Engagement = engagement;
            _engagementService = engagementService;
            _customerService = customerService;
            _closingPeriodService = closingPeriodService;
            _messenger = messenger;

            EngagementId = engagement.EngagementId;
            Description = engagement.Description;
            CustomerKey = engagement.CustomerKey;
            Currency = engagement.Currency;
            OpeningMargin = engagement.OpeningMargin;
            OpeningValue = engagement.OpeningValue;
            OpeningExpenses = engagement.OpeningExpenses;
            Status = engagement.Status;
            TotalPlannedHours = engagement.TotalPlannedHours;
            InitialHoursBudget = engagement.InitialHoursBudget;
            EtcpHours = engagement.EtcpHours;
            ValueEtcp = engagement.ValueEtcp;
            ExpensesEtcp = engagement.ExpensesEtcp;
            MarginPctEtcp = engagement.MarginPctEtcp;
            MarginPctBudget = engagement.MarginPctBudget;
            EtcpAgeDays = engagement.EtcpAgeDays;
            LatestEtcDate = engagement.LatestEtcDate;
            NextEtcDate = engagement.NextEtcDate;
            StatusText = engagement.StatusText;
            LastClosingPeriodId = engagement.LastClosingPeriodId;
            PapdAssignments = new ObservableCollection<EngagementPapd>(
                engagement.EngagementPapds
                    .OrderBy(p => p.EffectiveDate)
                    .ThenBy(p => p.Papd?.Name, StringComparer.OrdinalIgnoreCase));
            InitializeFinancialEvolutionEntries(engagement.FinancialEvolutions);

            _ = LoadCustomersAsync();
            _ = LoadClosingPeriodsAsync();
        }

        private async Task LoadCustomersAsync()
        {
            Customers = new ObservableCollection<Customer>(await _customerService.GetAllAsync());
            SelectedCustomer = Customers.FirstOrDefault(c => Engagement.CustomerId.HasValue && c.Id == Engagement.CustomerId)
                               ?? Customers.FirstOrDefault(c => c.Name == Engagement.CustomerKey);
        }

        private async Task LoadClosingPeriodsAsync()
        {
            var periods = await _closingPeriodService.GetAllAsync();
            var orderedPeriods = periods
                .OrderBy(p => !string.Equals(p.Name, "Initial", StringComparison.OrdinalIgnoreCase))
                .ThenBy(p => p.PeriodStart)
                .ToList();

            ClosingPeriods.Clear();
            foreach (var period in orderedPeriods)
            {
                ClosingPeriods.Add(period);
            }

            RefreshFinancialEvolutionSelections();
            SortFinancialEvolutionEntries();
        }

        private void InitializeFinancialEvolutionEntries(IEnumerable<FinancialEvolution> evolutions)
        {
            var entries = evolutions?
                    .Select(CreateEntry)
                ?? Enumerable.Empty<EngagementFinancialEvolutionEntryViewModel>();

            FinancialEvolutionEntries = new ObservableCollection<EngagementFinancialEvolutionEntryViewModel>(entries);
            RefreshFinancialEvolutionSelections();
            SortFinancialEvolutionEntries();
            SelectedFinancialEvolutionEntry = FinancialEvolutionEntries.FirstOrDefault();
        }

        private EngagementFinancialEvolutionEntryViewModel CreateEntry(FinancialEvolution evolution)
        {
            var entry = new EngagementFinancialEvolutionEntryViewModel(ClosingPeriods)
            {
                Id = evolution.Id,
                ClosingPeriodId = evolution.ClosingPeriodId ?? string.Empty,
                Hours = evolution.HoursData,
                Value = evolution.ValueData,
                Margin = evolution.MarginData,
                Expenses = evolution.ExpenseData
            };

            return entry;
        }

        private void RefreshFinancialEvolutionSelections()
        {
            foreach (var entry in FinancialEvolutionEntries)
            {
                entry.RefreshSelection();
            }
        }

        [RelayCommand]
        private void AddFinancialEvolutionEntry()
        {
            var entry = new EngagementFinancialEvolutionEntryViewModel(ClosingPeriods);
            if (ClosingPeriods.Any())
            {
                entry.SelectedClosingPeriod = ClosingPeriods.First();
            }
            else if (!string.IsNullOrWhiteSpace(LastClosingPeriodId))
            {
                entry.ClosingPeriodId = LastClosingPeriodId!;
            }

            FinancialEvolutionEntries.Add(entry);
            SelectedFinancialEvolutionEntry = entry;
            SortFinancialEvolutionEntries();
        }

        [RelayCommand(CanExecute = nameof(CanRemoveFinancialEvolutionEntry))]
        private void RemoveFinancialEvolutionEntry()
        {
            if (SelectedFinancialEvolutionEntry is null)
            {
                return;
            }

            FinancialEvolutionEntries.Remove(SelectedFinancialEvolutionEntry);
            SelectedFinancialEvolutionEntry = FinancialEvolutionEntries.LastOrDefault();
            SortFinancialEvolutionEntries();
        }

        private bool CanRemoveFinancialEvolutionEntry() => SelectedFinancialEvolutionEntry is not null;

        [RelayCommand]
        private async Task Save()
        {
            Engagement.EngagementId = EngagementId.Trim();
            Engagement.Description = Description;
            Engagement.CustomerKey = CustomerKey?.Trim() ?? string.Empty;
            Engagement.CustomerId = SelectedCustomer?.Id;
            Engagement.Currency = Currency?.Trim() ?? string.Empty;
            Engagement.OpeningMargin = OpeningMargin;
            Engagement.OpeningValue = OpeningValue;
            Engagement.OpeningExpenses = OpeningExpenses;
            Engagement.Status = Status;
            Engagement.StatusText = string.IsNullOrWhiteSpace(StatusText) ? null : StatusText.Trim();
            Engagement.TotalPlannedHours = TotalPlannedHours;
            Engagement.InitialHoursBudget = InitialHoursBudget;
            Engagement.EtcpHours = EtcpHours;
            Engagement.ValueEtcp = ValueEtcp;
            Engagement.ExpensesEtcp = ExpensesEtcp;
            Engagement.MarginPctEtcp = MarginPctEtcp;
            Engagement.MarginPctBudget = MarginPctBudget;
            Engagement.EtcpAgeDays = EtcpAgeDays;
            Engagement.LatestEtcDate = LatestEtcDate;
            Engagement.NextEtcDate = NextEtcDate;
            Engagement.LastClosingPeriodId = string.IsNullOrWhiteSpace(LastClosingPeriodId) ? null : LastClosingPeriodId.Trim();

            var papdAssignments = PapdAssignments
                .Where(a => a.PapdId != 0 || a.Papd?.Id > 0)
                .Select(a => new EngagementPapd
                {
                    Id = a.Id,
                    PapdId = a.PapdId != 0 ? a.PapdId : a.Papd!.Id,
                    EffectiveDate = a.EffectiveDate,
                    EngagementId = Engagement.Id,
                    Engagement = Engagement,
                    Papd = a.Papd
                })
                .ToList();

            Engagement.EngagementPapds = papdAssignments;

            Engagement.FinancialEvolutions = FinancialEvolutionEntries
                .Where(e => !string.IsNullOrWhiteSpace(e.ClosingPeriodId))
                .Select(e => new FinancialEvolution
                {
                    Id = e.Id,
                    ClosingPeriodId = e.ClosingPeriodId.Trim(),
                    EngagementId = Engagement.EngagementId,
                    Engagement = Engagement,
                    HoursData = e.Hours,
                    ValueData = e.Value,
                    MarginData = e.Margin,
                    ExpenseData = e.Expenses
                })
                .ToList();

            if (Engagement.Id == 0)
            {
                await _engagementService.AddAsync(Engagement);
            }
            else
            {
                await _engagementService.UpdateAsync(Engagement);
            }

            _messenger.Send(new CloseDialogMessage(true));
        }

        [RelayCommand]
        private void Close()
        {
            _messenger.Send(new CloseDialogMessage(false));
        }

        partial void OnLatestEtcDateChanged(DateTime? value)
        {
            OnPropertyChanged(nameof(LatestEtcDateOffset));
        }

        partial void OnNextEtcDateChanged(DateTime? value)
        {
            OnPropertyChanged(nameof(NextEtcDateOffset));
        }

        partial void OnSelectedCustomerChanged(Customer? value)
        {
            if (value is not null)
            {
                CustomerKey = string.IsNullOrWhiteSpace(value.CustomerID)
                    ? value.Name
                    : value.CustomerID;
            }
        }

        partial void OnSelectedFinancialEvolutionEntryChanged(EngagementFinancialEvolutionEntryViewModel? value)
        {
            RemoveFinancialEvolutionEntryCommand.NotifyCanExecuteChanged();
        }

        private void SortFinancialEvolutionEntries()
        {
            if (FinancialEvolutionEntries.Count <= 1)
            {
                return;
            }

            var sorted = FinancialEvolutionEntries
                .OrderBy(e => e.IsInitialClosingPeriod ? 0 : 1)
                .ThenBy(e => GetClosingPeriodIndex(e.ClosingPeriodId))
                .ThenBy(e => e.ClosingPeriodId, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (sorted.SequenceEqual(FinancialEvolutionEntries))
            {
                return;
            }

            var selectedEntry = SelectedFinancialEvolutionEntry;
            FinancialEvolutionEntries = new ObservableCollection<EngagementFinancialEvolutionEntryViewModel>(sorted);
            RefreshFinancialEvolutionSelections();

            if (selectedEntry is not null && FinancialEvolutionEntries.Contains(selectedEntry))
            {
                SelectedFinancialEvolutionEntry = selectedEntry;
            }
            else
            {
                SelectedFinancialEvolutionEntry = FinancialEvolutionEntries.FirstOrDefault();
            }
        }

        private int GetClosingPeriodIndex(string? closingPeriodId)
        {
            if (string.IsNullOrWhiteSpace(closingPeriodId))
            {
                return int.MaxValue;
            }

            for (var i = 0; i < ClosingPeriods.Count; i++)
            {
                if (string.Equals(ClosingPeriods[i].Name, closingPeriodId, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return int.MaxValue;
        }
    }
}