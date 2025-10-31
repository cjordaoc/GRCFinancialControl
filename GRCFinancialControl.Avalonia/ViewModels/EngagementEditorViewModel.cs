using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using App.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;
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
        private readonly int? _initialLastClosingPeriodId;

        private static readonly string[] DefaultStatusTextChoices =
        {
            "Active",
            "Inactive",
            "Closing",
            "Closed"
        };

        public sealed record EngagementSourceOption(EngagementSource Value, string DisplayName);

        private static readonly IReadOnlyList<EngagementSourceOption> DefaultSourceOptions = new[]
        {
            new EngagementSourceOption(EngagementSource.GrcProject, "GRC Project"),
            new EngagementSourceOption(EngagementSource.S4Project, "S/4 Project")
        };

        [ObservableProperty]
        private string _engagementId = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private Customer? _selectedCustomer;

        [ObservableProperty]
        private ObservableCollection<Customer> _customers = new();

        [ObservableProperty]
        private string _currency = string.Empty;

        [ObservableProperty]
        private ObservableCollection<EngagementSourceOption> _sourceOptions = new(DefaultSourceOptions);

        [ObservableProperty]
        private EngagementSourceOption? _selectedSource;

        [ObservableProperty]
        private decimal _openingValue;

        [ObservableProperty]
        private decimal _openingExpenses;

        [ObservableProperty]
        private decimal _initialHoursBudget;

        [ObservableProperty]
        private decimal _estimatedToCompleteHours;

        [ObservableProperty]
        private decimal _valueEtcp;

        [ObservableProperty]
        private decimal _expensesEtcp;

        [ObservableProperty]
        private decimal? _marginPctEtcp;

        [ObservableProperty]
        private decimal? _marginPctBudget;

        public int? EtcpAgeDays => CalculateEtcpAgeDays(LastEtcDate);

        [ObservableProperty]
        private DateTime? _lastEtcDate;

        [ObservableProperty]
        private DateTime? _proposedNextEtcDate;

        [ObservableProperty]
        private ObservableCollection<string> _statusTextOptions = new(DefaultStatusTextChoices);

        [ObservableProperty]
        private string? _selectedStatusText;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(LastClosingPeriodDisplay))]
        private ClosingPeriod? _lastClosingPeriod;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(LastClosingPeriodDisplay))]
        private string? _lastClosingPeriodFallbackName;

        [ObservableProperty]
        private ObservableCollection<EngagementPapd> _papdAssignments = new();

        [ObservableProperty]
        private ObservableCollection<ClosingPeriod> _closingPeriods = new();

        [ObservableProperty]
        private ObservableCollection<EngagementFinancialEvolutionEntryViewModel> _financialEvolutionEntries = new();

        [ObservableProperty]
        private EngagementFinancialEvolutionEntryViewModel? _selectedFinancialEvolutionEntry;

        [ObservableProperty]
        private ObservableCollection<EngagementManagerAssignment> _managerAssignments = new();

        [ObservableProperty]
        private bool _isReadOnlyMode;

        public Engagement Engagement { get; }

        public bool IsExistingRecord => Engagement.Id != 0;

        private bool ShouldLockForClosedStatus =>
            IsExistingRecord && MapStatusFromText(SelectedStatusText) == EngagementStatus.Closed;

        public bool AllowEditing => !IsReadOnlyMode && !ShouldLockForClosedStatus;

        public bool CanSave => !IsReadOnlyMode;

        public bool IsEngagementIdReadOnly => IsReadOnlyMode || IsExistingRecord;

        public bool CanEditEngagementId => !IsEngagementIdReadOnly;

        public bool IsFinancialSnapshotReadOnly => IsReadOnlyMode || IsExistingRecord;

        public bool IsGeneralFieldReadOnly => IsReadOnlyMode || ShouldLockForClosedStatus;

        public bool IsFinancialEvolutionReadOnly => IsReadOnlyMode || ShouldLockForClosedStatus;

        public bool CanEditCustomer => AllowEditing;

        public bool CanEditStatus => !IsReadOnlyMode;

        public bool IsCurrencyReadOnly => IsReadOnlyMode || IsExistingRecord;

        public string LastClosingPeriodDisplay => LastClosingPeriod?.Name ?? LastClosingPeriodFallbackName ?? string.Empty;

        public EngagementEditorViewModel(
            Engagement engagement,
            IEngagementService engagementService,
            ICustomerService customerService,
            IClosingPeriodService closingPeriodService,
            IMessenger messenger,
            bool isReadOnlyMode = false)
        {
            _initialLastClosingPeriodId = engagement.LastClosingPeriodId;
            Engagement = engagement;
            _engagementService = engagementService;
            _customerService = customerService;
            _closingPeriodService = closingPeriodService;
            _messenger = messenger;

            EngagementId = engagement.EngagementId;
            Description = engagement.Description;
            Currency = engagement.Currency;
            OpeningValue = engagement.OpeningValue;
            OpeningExpenses = engagement.OpeningExpenses;
            InitialHoursBudget = engagement.InitialHoursBudget;
            EstimatedToCompleteHours = engagement.EstimatedToCompleteHours;
            ValueEtcp = engagement.ValueEtcp;
            ExpensesEtcp = engagement.ExpensesEtcp;
            MarginPctEtcp = engagement.MarginPctEtcp;
            MarginPctBudget = engagement.MarginPctBudget;
            LastEtcDate = engagement.LastEtcDate;
            if (engagement.ProposedNextEtcDate.HasValue)
            {
                ProposedNextEtcDate = engagement.ProposedNextEtcDate;
            }
            LastClosingPeriod = engagement.LastClosingPeriod;
            LastClosingPeriodFallbackName = string.IsNullOrWhiteSpace(engagement.LastClosingPeriodName)
                ? null
                : engagement.LastClosingPeriodName;
            InitializeStatusSelection(engagement.StatusText, engagement.Status);
            InitializeSourceSelection(engagement.Source);
            PapdAssignments = new ObservableCollection<EngagementPapd>(
                engagement.EngagementPapds
                    .OrderBy(p => p.Papd?.Name, StringComparer.OrdinalIgnoreCase));
            ManagerAssignments = new ObservableCollection<EngagementManagerAssignment>(
                engagement.ManagerAssignments
                    .OrderBy(a => a.Manager?.Name, StringComparer.OrdinalIgnoreCase));
            InitializeFinancialEvolutionEntries(engagement.FinancialEvolutions);

            _ = LoadCustomersAsync();
            _ = LoadClosingPeriodsAsync();

            IsReadOnlyMode = isReadOnlyMode;
        }

        private async Task LoadCustomersAsync()
        {
            Customers = new ObservableCollection<Customer>(await _customerService.GetAllAsync());
            Customer? selected = null;

            if (Engagement.CustomerId.HasValue)
            {
                selected = Customers.FirstOrDefault(c => c.Id == Engagement.CustomerId);
            }

            if (selected is null && !string.IsNullOrWhiteSpace(Engagement.Customer?.Name))
            {
                var engagementCustomerName = Engagement.Customer!.Name;
                selected = Customers.FirstOrDefault(c =>
                    string.Equals(c.Name, engagementCustomerName, StringComparison.OrdinalIgnoreCase));
            }

            SelectedCustomer = selected;
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

            if (LastClosingPeriod is null && _initialLastClosingPeriodId.HasValue)
            {
                var matched = orderedPeriods.FirstOrDefault(p => p.Id == _initialLastClosingPeriodId.Value);
                if (matched is not null)
                {
                    LastClosingPeriod = matched;
                    LastClosingPeriodFallbackName = matched.Name;
                }
            }
            else if (LastClosingPeriod is not null)
            {
                var matched = orderedPeriods.FirstOrDefault(p => p.Id == LastClosingPeriod.Id);
                if (matched is not null)
                {
                    LastClosingPeriod = matched;
                }
            }

            RefreshFinancialEvolutionSelections();
            SortFinancialEvolutionEntries();
        }

        private void InitializeStatusSelection(string? statusText, EngagementStatus status)
        {
            StatusTextOptions = new ObservableCollection<string>(DefaultStatusTextChoices);

            var initialStatusText = DetermineInitialStatusText(statusText, status);

            if (!string.IsNullOrWhiteSpace(initialStatusText) &&
                StatusTextOptions.All(option =>
                    !string.Equals(option, initialStatusText, StringComparison.OrdinalIgnoreCase)))
            {
                StatusTextOptions.Add(initialStatusText);
            }

            SelectedStatusText = initialStatusText;
        }

        private static string DetermineInitialStatusText(string? statusText, EngagementStatus status)
        {
            if (!string.IsNullOrWhiteSpace(statusText))
            {
                return statusText.Trim();
            }

            return status switch
            {
                EngagementStatus.Inactive => "Inactive",
                EngagementStatus.Closed => "Closed",
                _ => "Active"
            };
        }

        private static EngagementStatus MapStatusFromText(string? statusText)
        {
            if (string.IsNullOrWhiteSpace(statusText))
            {
                return EngagementStatus.Active;
            }

            return statusText.Trim().ToLowerInvariant() switch
            {
                "closing" => EngagementStatus.Closed,
                "closed" => EngagementStatus.Closed,
                "inactive" => EngagementStatus.Inactive,
                _ => EngagementStatus.Active
            };
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
            UpdateLastClosingPeriodFromEntries();
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
            else if (!string.IsNullOrWhiteSpace(LastClosingPeriodDisplay))
            {
                entry.ClosingPeriodId = LastClosingPeriodDisplay;
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
            if (IsReadOnlyMode)
            {
                return;
            }

            try
            {
                Engagement.EngagementId = EngagementId.Trim();
                Engagement.Description = Description;
                Engagement.CustomerId = SelectedCustomer?.Id;
                Engagement.Customer = SelectedCustomer;
                Engagement.Currency = Currency?.Trim() ?? string.Empty;
                Engagement.OpeningValue = OpeningValue;
                Engagement.OpeningExpenses = OpeningExpenses;
                var normalizedStatusText = string.IsNullOrWhiteSpace(SelectedStatusText)
                    ? null
                    : SelectedStatusText.Trim();

                Engagement.StatusText = normalizedStatusText;
                Engagement.Status = MapStatusFromText(normalizedStatusText);
                Engagement.InitialHoursBudget = InitialHoursBudget;
                Engagement.EstimatedToCompleteHours = EstimatedToCompleteHours;
                Engagement.ValueEtcp = ValueEtcp;
                Engagement.ExpensesEtcp = ExpensesEtcp;
                Engagement.MarginPctEtcp = MarginPctEtcp;
                Engagement.MarginPctBudget = MarginPctBudget;
                Engagement.LastEtcDate = LastEtcDate;
                Engagement.ProposedNextEtcDate = ProposedNextEtcDate;
                Engagement.LastClosingPeriodId = LastClosingPeriod?.Id;
                Engagement.LastClosingPeriod = LastClosingPeriod;
                Engagement.Source = (SelectedSource?.Value) ?? EngagementSource.GrcProject;

                var papdAssignments = PapdAssignments
                    .Where(a => a.PapdId != 0 || a.Papd?.Id > 0)
                    .Select(a => new EngagementPapd
                    {
                        Id = a.Id,
                        PapdId = a.PapdId != 0 ? a.PapdId : a.Papd!.Id,
                        EngagementId = Engagement.Id,
                        Engagement = Engagement,
                        Papd = a.Papd
                    })
                    .ToList();

                var engagementPapds = Engagement.EngagementPapds;
                engagementPapds.Clear();
                foreach (var assignment in papdAssignments)
                {
                    engagementPapds.Add(assignment);
                }

                var evolutions = Engagement.FinancialEvolutions;
                evolutions.Clear();
                foreach (var evolution in FinancialEvolutionEntries
                             .Where(e => !string.IsNullOrWhiteSpace(e.ClosingPeriodId))
                             .Select(e => new FinancialEvolution
                             {
                                 Id = e.Id,
                                 ClosingPeriodId = e.ClosingPeriodId.Trim(),
                                 EngagementId = Engagement.Id,
                                 Engagement = Engagement,
                                 HoursData = e.Hours,
                                 ValueData = e.Value,
                                 MarginData = e.Margin,
                                 ExpenseData = e.Expenses
                             }))
                {
                    evolutions.Add(evolution);
                }

                if (Engagement.Id == 0)
                {
                    await _engagementService.AddAsync(Engagement);
                }
                else
                {
                    await _engagementService.UpdateAsync(Engagement);
                }

                ToastService.ShowSuccess("Engagements.Toast.Saved", Engagement.EngagementId);
                _messenger.Send(new CloseDialogMessage(true));
            }
            catch (InvalidOperationException)
            {
                ToastService.ShowWarning("Engagements.Toast.SaveFailed");
            }
            catch (Exception)
            {
                ToastService.ShowError("Engagements.Toast.SaveFailed");
            }
        }

        private void InitializeSourceSelection(EngagementSource source)
        {
            SourceOptions = new ObservableCollection<EngagementSourceOption>(DefaultSourceOptions);
            SelectedSource = SourceOptions.FirstOrDefault(option => option.Value == source)
                ?? SourceOptions.FirstOrDefault();
        }

        [RelayCommand]
        private void Close()
        {
            _messenger.Send(new CloseDialogMessage(false));
        }

        partial void OnLastEtcDateChanged(DateTime? value)
        {
            UpdateSchedulingFields();
        }



        partial void OnSelectedFinancialEvolutionEntryChanged(EngagementFinancialEvolutionEntryViewModel? value)
        {
            RemoveFinancialEvolutionEntryCommand.NotifyCanExecuteChanged();
        }

        partial void OnIsReadOnlyModeChanged(bool value)
        {
            RefreshEditingState();
        }

        partial void OnSelectedStatusTextChanged(string? value)
        {
            RefreshEditingState();
        }

        private void RefreshEditingState()
        {
            OnPropertyChanged(nameof(AllowEditing));
            OnPropertyChanged(nameof(CanSave));
            OnPropertyChanged(nameof(IsEngagementIdReadOnly));
            OnPropertyChanged(nameof(CanEditEngagementId));
            OnPropertyChanged(nameof(IsFinancialSnapshotReadOnly));
            OnPropertyChanged(nameof(IsGeneralFieldReadOnly));
            OnPropertyChanged(nameof(IsFinancialEvolutionReadOnly));
            OnPropertyChanged(nameof(CanEditCustomer));
            OnPropertyChanged(nameof(CanEditStatus));
            OnPropertyChanged(nameof(IsCurrencyReadOnly));
        }

        private void UpdateSchedulingFields()
        {
            var proposed = CalculateProposedNextEtcDate(LastEtcDate);
            ProposedNextEtcDate = proposed;
            OnPropertyChanged(nameof(EtcpAgeDays));
        }

        private static int? CalculateEtcpAgeDays(DateTime? lastEtcDate)
        {
            if (!lastEtcDate.HasValue)
            {
                return null;
            }

            var today = DateTime.UtcNow.Date;
            var lastDate = lastEtcDate.Value.Date;
            var age = (today - lastDate).Days;
            return age < 0 ? 0 : age;
        }

        private static DateTime? CalculateProposedNextEtcDate(DateTime? lastEtcDate)
        {
            if (!lastEtcDate.HasValue)
            {
                return null;
            }

            var proposal = lastEtcDate.Value.Date.AddMonths(1);
            return DateTime.SpecifyKind(proposal, DateTimeKind.Unspecified);
        }

        private void SortFinancialEvolutionEntries()
        {
            UpdateLastClosingPeriodFromEntries();

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

            UpdateLastClosingPeriodFromEntries();
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

        private void UpdateLastClosingPeriodFromEntries()
        {
            if (FinancialEvolutionEntries.Count == 0)
            {
                LastClosingPeriod = null;
                LastClosingPeriodFallbackName = null;
                return;
            }

            var latest = FinancialEvolutionEntries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.ClosingPeriodId))
                .Where(entry => !entry.IsInitialClosingPeriod)
                .Select(entry =>
                {
                    var normalizedId = entry.ClosingPeriodId.Trim();
                    var index = GetClosingPeriodIndex(normalizedId);
                    var sortIndex = index == int.MaxValue ? int.MaxValue : index;
                    return (entry, normalizedId, sortIndex);
                })
                .OrderByDescending(tuple => tuple.sortIndex)
                .ThenByDescending(tuple => tuple.normalizedId, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (latest.entry is null)
            {
                LastClosingPeriod = null;
                LastClosingPeriodFallbackName = null;
                return;
            }

            var matched = ClosingPeriods.FirstOrDefault(p =>
                string.Equals(p.Name, latest.normalizedId, StringComparison.OrdinalIgnoreCase));

            if (matched is not null)
            {
                LastClosingPeriod = matched;
                LastClosingPeriodFallbackName = matched.Name;
            }
            else
            {
                LastClosingPeriod = null;
                LastClosingPeriodFallbackName = latest.normalizedId;
            }
        }
    }
}
