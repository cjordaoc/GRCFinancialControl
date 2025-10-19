using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using App.Presentation.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class AllocationEditorViewModel : ViewModelBase
    {
        private const decimal VarianceTolerance = 0.01m;

        private readonly IEngagementService _engagementService;
        private readonly AllocationKind _kind;

        [ObservableProperty]
        private Engagement _engagement;

        [ObservableProperty]
        private ObservableCollection<AllocationEntry> _allocations;

        [ObservableProperty]
        private decimal _targetAmount;

        [ObservableProperty]
        private decimal _currentAllocation;

        [ObservableProperty]
        private decimal _allocationVariance;

        [ObservableProperty]
        private bool _hasAllocationVariance;

        [ObservableProperty]
        private string? _validationMessage;

        [ObservableProperty]
        private string _targetAmountDisplay = string.Empty;

        [ObservableProperty]
        private string _currentAllocationDisplay = string.Empty;

        [ObservableProperty]
        private string _allocationVarianceDisplay = string.Empty;

        [ObservableProperty]
        private string _allocationVarianceTooltip = string.Empty;

        [ObservableProperty]
        private string? _statusMessage;

        [ObservableProperty]
        private bool _isReadOnlyMode;

        public AllocationEditorViewModel(Engagement engagement,
                                         List<FiscalYear> fiscalYears,
                                         IEngagementService engagementService,
                                         IMessenger messenger,
                                         AllocationKind kind,
                                         bool isReadOnlyMode = false)
            : base(messenger ?? throw new ArgumentNullException(nameof(messenger)))
        {
            ArgumentNullException.ThrowIfNull(engagement);
            ArgumentNullException.ThrowIfNull(fiscalYears);
            ArgumentNullException.ThrowIfNull(engagementService);

            _engagement = engagement;
            _engagementService = engagementService;
            _kind = kind;

            TargetAmount = GetTargetAmount(engagement);

            Allocations = new ObservableCollection<AllocationEntry>(
                fiscalYears.Select(fy => CreateAllocationEntry(engagement, fy))
            );

            CurrentAllocation = Allocations.Sum(a => a.TotalAmount);
            UpdateVariance();

            foreach (var allocation in Allocations)
            {
                allocation.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName is nameof(AllocationEntry.PlannedAmount)
                        or nameof(AllocationEntry.ToDateAmount)
                        or nameof(AllocationEntry.ToGoAmount))
                    {
                        CurrentAllocation = Allocations.Sum(a => a.TotalAmount);
                    }
                };
            }

            IsReadOnlyMode = isReadOnlyMode;
        }

        public string TargetLabel => LocalizationRegistry.Get(
            _kind == AllocationKind.Hours
                ? "Allocations.Label.TargetHours"
                : "Allocations.Label.TargetValue");

        public string CurrentAllocationLabel => LocalizationRegistry.Get(
            _kind == AllocationKind.Hours
                ? "Allocations.Label.CurrentHours"
                : "Allocations.Label.CurrentValue");

        public string AmountColumnHeader => LocalizationRegistry.Get("Allocations.Header.PlannedHours");

        public string ToDateColumnHeader => LocalizationRegistry.Get("Allocations.Header.ToDateValue");

        public string ToGoColumnHeader => LocalizationRegistry.Get("Allocations.Header.ToGoValue");

        public string ValidationErrorMessage => LocalizationRegistry.Get(
            _kind == AllocationKind.Hours
                ? "Allocations.Validation.HoursMatchTarget"
                : "Allocations.Validation.ValueMatchTarget");

        public bool AllowEditing => !IsReadOnlyMode;

        public bool IsRevenueMode => _kind == AllocationKind.Revenue;

        public bool IsHoursMode => _kind == AllocationKind.Hours;

        [RelayCommand(CanExecute = nameof(CanSave))]
        private async Task SaveAsync()
        {
            if (!CanSave())
            {
                return;
            }

            StatusMessage = null;

            if (_kind == AllocationKind.Hours)
            {
                Engagement.Allocations.Clear();
                foreach (var allocation in Allocations)
                {
                    Engagement.Allocations.Add(new EngagementFiscalYearAllocation
                    {
                        EngagementId = Engagement.Id,
                        FiscalYearId = allocation.FiscalYear.Id,
                        PlannedHours = allocation.PlannedAmount
                    });
                }
            }
            else
            {
                Engagement.RevenueAllocations.Clear();
                foreach (var allocation in Allocations)
                {
                    Engagement.RevenueAllocations.Add(new EngagementFiscalYearRevenueAllocation
                    {
                        EngagementId = Engagement.Id,
                        FiscalYearId = allocation.FiscalYear.Id,
                        ToGoValue = decimal.Round(allocation.ToGoAmount, 2),
                        ToDateValue = decimal.Round(allocation.ToDateAmount, 2)
                    });
                }
            }

            try
            {
                await _engagementService.UpdateAsync(Engagement);
                Messenger.Send(new CloseDialogMessage(true));
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = ex.Message;
            }
        }

        [RelayCommand]
        private void Close()
        {
            Messenger.Send(new CloseDialogMessage(false));
        }

        partial void OnCurrentAllocationChanged(decimal value)
        {
            UpdateVariance();
            CurrentAllocationDisplay = FormatAmount(value);
        }

        partial void OnTargetAmountChanged(decimal value)
        {
            UpdateVariance();
            TargetAmountDisplay = FormatAmount(value);
        }

        partial void OnAllocationVarianceChanged(decimal value)
        {
            var suffix = GetVarianceSuffix();
            AllocationVarianceDisplay = value == 0
                ? string.Empty
                : string.Format(CultureInfo.InvariantCulture, "{0:+0.##;-0.##;0}{1}", value, suffix);
            AllocationVarianceTooltip = LocalizationRegistry.Format("Allocations.Tooltip.Variance", value, suffix);
        }

        private void UpdateVariance()
        {
            AllocationVariance = CurrentAllocation - TargetAmount;
            HasAllocationVariance = Math.Abs(AllocationVariance) > VarianceTolerance;
            ValidationMessage = HasAllocationVariance ? ValidationErrorMessage : null;
        }

        private bool CanSave() => !HasAllocationVariance && !IsReadOnlyMode;

        partial void OnHasAllocationVarianceChanged(bool value)
        {
            SaveCommand.NotifyCanExecuteChanged();
        }

        partial void OnIsReadOnlyModeChanged(bool value)
        {
            SaveCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(AllowEditing));
        }

        private decimal GetTargetAmount(Engagement engagement)
        {
            return _kind == AllocationKind.Hours ? engagement.HoursToAllocate : engagement.ValueToAllocate;
        }

        private AllocationEntry CreateAllocationEntry(Engagement engagement, FiscalYear fiscalYear)
        {
            if (_kind == AllocationKind.Hours)
            {
                return new AllocationEntry
                {
                    FiscalYear = fiscalYear,
                    PlannedAmount = engagement.Allocations.FirstOrDefault(a => a.FiscalYearId == fiscalYear.Id)?.PlannedHours ?? 0m,
                    IsLocked = fiscalYear.IsLocked,
                    IsRevenue = false
                };
            }

            var allocation = engagement.RevenueAllocations.FirstOrDefault(a => a.FiscalYearId == fiscalYear.Id);

            return new AllocationEntry
            {
                FiscalYear = fiscalYear,
                IsLocked = fiscalYear.IsLocked,
                IsRevenue = true,
                ToDateAmount = allocation?.ToDateValue ?? 0m,
                ToGoAmount = allocation?.ToGoValue ?? 0m
            };
        }

        private string GetVarianceSuffix()
        {
            if (_kind == AllocationKind.Hours)
            {
                return LocalizationRegistry.Get("Common.Unit.HoursSuffix");
            }

            return string.IsNullOrWhiteSpace(Engagement.Currency)
                ? string.Empty
                : string.Format(CultureInfo.InvariantCulture, " {0}", Engagement.Currency);
        }

        private string FormatAmount(decimal value)
        {
            var formatted = value.ToString("N2", CultureInfo.InvariantCulture);

            if (_kind == AllocationKind.Hours)
            {
                return formatted;
            }

            return string.IsNullOrWhiteSpace(Engagement.Currency)
                ? formatted
                : string.Format(CultureInfo.InvariantCulture, "{0} {1}", Engagement.Currency, formatted);
        }
    }

    public partial class AllocationEntry : ObservableObject
    {
        [ObservableProperty]
        private FiscalYear _fiscalYear = null!;

        [ObservableProperty]
        private decimal _plannedAmount;

        [ObservableProperty]
        private decimal _toDateAmount;

        [ObservableProperty]
        private decimal _toGoAmount;

        [ObservableProperty]
        private bool _isLocked;

        [ObservableProperty]
        private bool _isRevenue;

        partial void OnPlannedAmountChanged(decimal value)
        {
            OnPropertyChanged(nameof(TotalAmount));
        }

        partial void OnToDateAmountChanged(decimal value)
        {
            OnPropertyChanged(nameof(TotalAmount));
        }

        partial void OnToGoAmountChanged(decimal value)
        {
            OnPropertyChanged(nameof(TotalAmount));
        }

        partial void OnIsLockedChanged(bool value)
        {
            OnPropertyChanged(nameof(IsEditable));
        }

        public bool IsEditable => !IsLocked;

        public decimal TotalAmount => IsRevenue ? ToDateAmount + ToGoAmount : PlannedAmount;
    }
}
