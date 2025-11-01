using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using App.Presentation.Localization;
using App.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;
using GRC.Shared.UI.Messages;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class AllocationEditorViewModel : ViewModelBase
    {
        private const decimal VarianceTolerance = 0.01m;

        private readonly IEngagementService _engagementService;

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
                                         bool isReadOnlyMode = false)
            : base(messenger ?? throw new ArgumentNullException(nameof(messenger)))
        {
            ArgumentNullException.ThrowIfNull(engagement);
            ArgumentNullException.ThrowIfNull(fiscalYears);
            ArgumentNullException.ThrowIfNull(engagementService);

            _engagement = engagement;
            _engagementService = engagementService;

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
                    if (e.PropertyName is nameof(AllocationEntry.ToDateAmount)
                        or nameof(AllocationEntry.ToGoAmount))
                    {
                        CurrentAllocation = Allocations.Sum(a => a.TotalAmount);
                    }
                };
            }

            IsReadOnlyMode = isReadOnlyMode;
        }

        public string TargetLabel => LocalizationRegistry.Get("Allocations.Label.TargetValue");

        public string CurrentAllocationLabel => LocalizationRegistry.Get("Allocations.Label.CurrentValue");

        public string ToDateColumnHeader => LocalizationRegistry.Get("Allocations.Header.ToDateValue");

        public string ToGoColumnHeader => LocalizationRegistry.Get("Allocations.Header.ToGoValue");

        public string ValidationErrorMessage => LocalizationRegistry.Get("Allocations.Validation.ValueMatchTarget");

        public bool AllowEditing => !IsReadOnlyMode;

        public string CurrencySymbol => CurrencyDisplayHelper.Resolve(Engagement.Currency).Symbol;

        public bool HasCurrencySymbol => !string.IsNullOrWhiteSpace(CurrencySymbol);

        [RelayCommand(CanExecute = nameof(CanSave))]
        private async Task SaveAsync()
        {
            if (!CanSave())
            {
                return;
            }

            StatusMessage = null;

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
            if (Math.Abs(value) < 0.005m)
            {
                AllocationVarianceDisplay = string.Empty;
                AllocationVarianceTooltip = LocalizationRegistry.Format("Allocations.Tooltip.Variance", CurrencyDisplayHelper.Format(0m, Engagement.Currency));
                return;
            }

            var formatted = CurrencyDisplayHelper.Format(Math.Abs(value), Engagement.Currency);
            AllocationVarianceDisplay = value > 0 ? string.Concat("+", formatted) : string.Concat("-", formatted);
            AllocationVarianceTooltip = LocalizationRegistry.Format("Allocations.Tooltip.Variance", AllocationVarianceDisplay);
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

        private static decimal GetTargetAmount(Engagement engagement) => engagement.ValueToAllocate;

        private AllocationEntry CreateAllocationEntry(Engagement engagement, FiscalYear fiscalYear)
        {
            var allocation = engagement.RevenueAllocations.FirstOrDefault(a => a.FiscalYearId == fiscalYear.Id);

            return new AllocationEntry
            {
                FiscalYear = fiscalYear,
                IsLocked = fiscalYear.IsLocked,
                ToDateAmount = allocation?.ToDateValue ?? 0m,
                ToGoAmount = allocation?.ToGoValue ?? 0m
            };
        }

        private string FormatAmount(decimal value)
        {
            return CurrencyDisplayHelper.Format(value, Engagement.Currency);
        }
    }

    public partial class AllocationEntry : ObservableObject
    {
        [ObservableProperty]
        private FiscalYear _fiscalYear = null!;

        [ObservableProperty]
        private decimal _toDateAmount;

        [ObservableProperty]
        private decimal _toGoAmount;

        [ObservableProperty]
        private bool _isLocked;

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

        public decimal TotalAmount => ToDateAmount + ToGoAmount;
    }
}
