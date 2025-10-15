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
        private readonly IEngagementService _engagementService;
        private readonly IMessenger _messenger;
        private readonly AllocationKind _kind;

        [ObservableProperty]
        private Engagement _engagement;

        [ObservableProperty]
        private ObservableCollection<AllocationEntry> _allocations;

        [ObservableProperty]
        private double _targetAmount;

        [ObservableProperty]
        private double _currentAllocation;

        [ObservableProperty]
        private double _allocationVariance;

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

        public AllocationEditorViewModel(Engagement engagement,
                                         List<FiscalYear> fiscalYears,
                                         IEngagementService engagementService,
                                         IMessenger messenger,
                                         AllocationKind kind)
        {
            _engagement = engagement;
            _engagementService = engagementService;
            _messenger = messenger;
            _kind = kind;

            TargetAmount = GetTargetAmount(engagement);

            Allocations = new ObservableCollection<AllocationEntry>(
                fiscalYears.Select(fy => new AllocationEntry
                {
                    FiscalYear = fy,
                    PlannedAmount = GetExistingAllocationAmount(engagement, fy.Id)
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
        }

        public string TargetLabel => _kind == AllocationKind.Hours ? "Hours to Allocate:" : "Value to Allocate:";

        public string CurrentAllocationLabel => _kind == AllocationKind.Hours ? "Current Hours Allocation:" : "Current Value Allocation:";

        public string AmountColumnHeader => _kind == AllocationKind.Hours ? "Planned Hours" : "Planned Value";

        public string ValidationErrorMessage => _kind == AllocationKind.Hours
            ? "Allocated hours must match the target hours before saving."
            : "Allocated value must match the target value before saving.";

        [RelayCommand]
        private async Task Save()
        {
            if (HasAllocationVariance)
            {
                return;
            }

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
                        PlannedValue = (decimal)Math.Round(allocation.PlannedAmount, 2)
                    });
                }
            }

            await _engagementService.UpdateAsync(Engagement);
            _messenger.Send(new CloseDialogMessage(true));
        }

        [RelayCommand]
        private void Close()
        {
            _messenger.Send(new CloseDialogMessage(false));
        }

        partial void OnCurrentAllocationChanged(double value)
        {
            UpdateVariance();
            CurrentAllocationDisplay = FormatAmount(value);
        }

        partial void OnTargetAmountChanged(double value)
        {
            UpdateVariance();
            TargetAmountDisplay = FormatAmount(value);
        }

        partial void OnAllocationVarianceChanged(double value)
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
            HasAllocationVariance = Math.Abs(AllocationVariance) > 0.01d;
            ValidationMessage = HasAllocationVariance ? ValidationErrorMessage : null;
        }

        private double GetTargetAmount(Engagement engagement)
        {
            return _kind == AllocationKind.Hours ? engagement.HoursToAllocate : engagement.ValueToAllocate;
        }

        private double GetExistingAllocationAmount(Engagement engagement, int fiscalYearId)
        {
            if (_kind == AllocationKind.Hours)
            {
                return engagement.Allocations.FirstOrDefault(a => a.FiscalYearId == fiscalYearId)?.PlannedHours ?? 0d;
            }

            return (double)(engagement.RevenueAllocations.FirstOrDefault(a => a.FiscalYearId == fiscalYearId)?.PlannedValue ?? 0m);
        }

        private string GetVarianceSuffix()
        {
            if (_kind == AllocationKind.Hours)
            {
                return " h";
            }

            return string.IsNullOrWhiteSpace(Engagement.Currency)
                ? string.Empty
                : $" {Engagement.Currency}";
        }

        private string FormatAmount(double value)
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
        private double _plannedAmount;
    }
}
