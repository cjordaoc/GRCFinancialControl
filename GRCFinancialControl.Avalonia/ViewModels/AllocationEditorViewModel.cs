using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
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
        private const string HoursTargetLabel = "Hours to Allocate:";
        private const string ValueTargetLabel = "Value to Allocate:";
        private const string HoursCurrentAllocationLabel = "Current Hours Allocation:";
        private const string ValueCurrentAllocationLabel = "Current Value Allocation:";
        private const string HoursAmountHeader = "Planned Hours";
        private const string ValueAmountHeader = "Planned Value";
        private const string HoursValidationMessage = "Allocated hours must match the target hours before saving.";
        private const string ValueValidationMessage = "Allocated value must match the target value before saving.";
        private const string HoursVarianceSuffix = " h";

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
                fiscalYears.Select(fy => new AllocationEntry
                {
                    FiscalYear = fy,
                    PlannedAmount = GetExistingAllocationAmount(engagement, fy.Id),
                    IsLocked = fy.IsLocked
                })
            );

            CurrentAllocation = Allocations.Sum(a => a.PlannedAmount);
            UpdateVariance();

            foreach (var allocation in Allocations)
            {
                allocation.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(AllocationEntry.PlannedAmount))
                    {
                        CurrentAllocation = Allocations.Sum(a => a.PlannedAmount);
                    }
                };
            }

            IsReadOnlyMode = isReadOnlyMode;
        }

        public string TargetLabel => _kind == AllocationKind.Hours ? HoursTargetLabel : ValueTargetLabel;

        public string CurrentAllocationLabel => _kind == AllocationKind.Hours ? HoursCurrentAllocationLabel : ValueCurrentAllocationLabel;

        public string AmountColumnHeader => _kind == AllocationKind.Hours ? HoursAmountHeader : ValueAmountHeader;

        public string ValidationErrorMessage => _kind == AllocationKind.Hours
            ? HoursValidationMessage
            : ValueValidationMessage;

        public bool AllowEditing => !IsReadOnlyMode;

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
                        ToGoValue = decimal.Round(allocation.PlannedAmount, 2),
                        ToDateValue = 0m
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
            AllocationVarianceTooltip = string.Format(CultureInfo.InvariantCulture, "Variance {0:+0.##;-0.##;0}{1}", value, suffix);
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

        private decimal GetExistingAllocationAmount(Engagement engagement, int fiscalYearId)
        {
            if (_kind == AllocationKind.Hours)
            {
                return engagement.Allocations.FirstOrDefault(a => a.FiscalYearId == fiscalYearId)?.PlannedHours ?? 0m;
            }

            var allocation = engagement.RevenueAllocations.FirstOrDefault(a => a.FiscalYearId == fiscalYearId);
            return allocation is null ? 0m : allocation.ToGoValue + allocation.ToDateValue;
        }

        private string GetVarianceSuffix()
        {
            if (_kind == AllocationKind.Hours)
            {
                return HoursVarianceSuffix;
            }

            return string.IsNullOrWhiteSpace(Engagement.Currency)
                ? string.Empty
                : $" {Engagement.Currency}";
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
        private bool _isLocked;

        partial void OnIsLockedChanged(bool value)
        {
            OnPropertyChanged(nameof(IsEditable));
        }

        public bool IsEditable => !IsLocked;
    }
}
