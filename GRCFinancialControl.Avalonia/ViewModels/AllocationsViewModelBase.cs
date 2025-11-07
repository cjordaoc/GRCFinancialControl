using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRC.Shared.UI.Messages;
using GRCFinancialControl.Avalonia.Services;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public abstract partial class AllocationsViewModelBase : ViewModelBase
    {
        private readonly IEngagementService _engagementService;
        private readonly IFiscalYearService _fiscalYearService;
        private readonly DialogService _dialogService;
        private readonly ICustomerService _customerService;
        private readonly IClosingPeriodService _closingPeriodService;
        private readonly IAllocationSnapshotService _allocationSnapshotService;

        [ObservableProperty]
        private ObservableCollection<Engagement> _engagements = new();

        [ObservableProperty]
        private ObservableCollection<FiscalYear> _fiscalYears = new();

        [ObservableProperty]
        private ObservableCollection<ClosingPeriod> _closingPeriods = new();

        [ObservableProperty]
        private Engagement? _selectedEngagement;

        [ObservableProperty]
        private ClosingPeriod? _selectedClosingPeriod;

        protected AllocationsViewModelBase(IEngagementService engagementService,
                                           IFiscalYearService fiscalYearService,
                                           ICustomerService customerService,
                                           IClosingPeriodService closingPeriodService,
                                           IAllocationSnapshotService allocationSnapshotService,
                                           DialogService dialogService,
                                           IMessenger messenger)
            : base(messenger)
        {
            _engagementService = engagementService;
            _fiscalYearService = fiscalYearService;
            _customerService = customerService;
            _closingPeriodService = closingPeriodService;
            _allocationSnapshotService = allocationSnapshotService;
            _dialogService = dialogService;
        }

        public abstract string Header { get; }

        public override async Task LoadDataAsync()
        {
            Engagements = new ObservableCollection<Engagement>(await _engagementService.GetAllAsync());
            FiscalYears = new ObservableCollection<FiscalYear>(await _fiscalYearService.GetAllAsync());
            ClosingPeriods = new ObservableCollection<ClosingPeriod>(await _closingPeriodService.GetAllAsync());
            
            // Select latest closing period by default
            SelectedClosingPeriod = ClosingPeriods.OrderByDescending(cp => cp.PeriodEnd).FirstOrDefault();
        }

        [RelayCommand]
        private async Task EditAllocation(Engagement engagement)
        {
            if (engagement == null || SelectedClosingPeriod == null)
            {
                return;
            }

            var editorViewModel = new AllocationEditorViewModel(
                engagement,
                SelectedClosingPeriod,
                FiscalYears.ToList(),
                _engagementService,
                _allocationSnapshotService,
                Messenger);
            await _dialogService.ShowDialogAsync(editorViewModel);
            Messenger.Send(new RefreshViewMessage(RefreshTargets.FinancialData));
        }

        [RelayCommand]
        private async Task ViewAllocation(Engagement engagement)
        {
            if (engagement == null || SelectedClosingPeriod == null)
            {
                return;
            }

            var editorViewModel = new AllocationEditorViewModel(
                engagement,
                SelectedClosingPeriod,
                FiscalYears.ToList(),
                _engagementService,
                _allocationSnapshotService,
                Messenger,
                isReadOnlyMode: true);
            await _dialogService.ShowDialogAsync(editorViewModel);
        }

        [RelayCommand]
        private async Task ViewEngagement(Engagement engagement)
        {
            if (engagement == null)
            {
                return;
            }

            var fullEngagement = await _engagementService.GetByIdAsync(engagement.Id);
            if (fullEngagement is null)
            {
                return;
            }

            var editorViewModel = new EngagementEditorViewModel(
                fullEngagement,
                _engagementService,
                _customerService,
                _closingPeriodService,
                Messenger,
                _dialogService,
                isReadOnlyMode: true);
            await _dialogService.ShowDialogAsync(editorViewModel);
        }
    }
}
