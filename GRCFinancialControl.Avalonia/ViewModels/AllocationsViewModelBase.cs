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
        private readonly IEngagementManagementFacade _engagementFacade;
        private readonly IFiscalYearService _fiscalYearService;
        private readonly DialogService _dialogService;
        private readonly IAllocationSnapshotService _allocationSnapshotService;
        private readonly ISettingsService _settingsService;

        [ObservableProperty]
        private ObservableCollection<Engagement> _engagements = new();

        [ObservableProperty]
        private ObservableCollection<FiscalYear> _fiscalYears = new();

        [ObservableProperty]
        private Engagement? _selectedEngagement;

        protected AllocationsViewModelBase(IEngagementManagementFacade engagementFacade,
                                           IFiscalYearService fiscalYearService,
                                           IAllocationSnapshotService allocationSnapshotService,
                                           ISettingsService settingsService,
                                           DialogService dialogService,
                                           IMessenger messenger)
            : base(messenger)
        {
            _engagementFacade = engagementFacade;
            _fiscalYearService = fiscalYearService;
            _allocationSnapshotService = allocationSnapshotService;
            _settingsService = settingsService;
            _dialogService = dialogService;
        }

        public abstract string Header { get; }

        public override async Task LoadDataAsync()
        {
            Engagements = new ObservableCollection<Engagement>(await _engagementFacade.GetAllEngagementsAsync());
            FiscalYears = new ObservableCollection<FiscalYear>(await _fiscalYearService.GetAllAsync());
        }

        /// <summary>
        /// Gets the currently selected global closing period from settings.
        /// Returns null if no period is selected.
        /// </summary>
        private async Task<ClosingPeriod?> GetCurrentClosingPeriodAsync()
        {
            var closingPeriodId = await _settingsService.GetDefaultClosingPeriodIdAsync()
                .ConfigureAwait(false);
            
            if (!closingPeriodId.HasValue)
            {
                // TODO: Consider showing user notification that closing period must be selected
                return null;
            }

            var allPeriods = await _engagementFacade.GetAllClosingPeriodsAsync()
                .ConfigureAwait(false);
            
            return allPeriods.FirstOrDefault(p => p.Id == closingPeriodId.Value);
        }

        [RelayCommand]
        private async Task EditAllocation(Engagement engagement)
        {
            if (engagement == null)
            {
                return;
            }

            var closingPeriod = await GetCurrentClosingPeriodAsync();
            if (closingPeriod == null)
            {
                return;
            }

            var editorViewModel = new AllocationEditorViewModel(
                engagement,
                closingPeriod,
                FiscalYears.ToList(),
                _engagementFacade.EngagementService,
                _allocationSnapshotService,
                Messenger);
            await _dialogService.ShowDialogAsync(editorViewModel);
            Messenger.Send(new RefreshViewMessage(RefreshTargets.FinancialData));
        }

        [RelayCommand]
        private async Task ViewAllocation(Engagement engagement)
        {
            if (engagement == null)
            {
                return;
            }

            var closingPeriod = await GetCurrentClosingPeriodAsync();
            if (closingPeriod == null)
            {
                return;
            }

            var editorViewModel = new AllocationEditorViewModel(
                engagement,
                closingPeriod,
                FiscalYears.ToList(),
                _engagementFacade.EngagementService,
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

            var fullEngagement = await _engagementFacade.GetEngagementAsync(engagement.Id);
            if (fullEngagement is null)
            {
                return;
            }

            var editorViewModel = new EngagementEditorViewModel(
                fullEngagement,
                _engagementFacade,
                Messenger,
                _dialogService,
                isReadOnlyMode: true);
            await _dialogService.ShowDialogAsync(editorViewModel);
        }
    }
}
