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

        [ObservableProperty]
        private ObservableCollection<Engagement> _engagements = new();

        [ObservableProperty]
        private ObservableCollection<FiscalYear> _fiscalYears = new();

        [ObservableProperty]
        private Engagement? _selectedEngagement;

        protected AllocationsViewModelBase(IEngagementService engagementService,
                                           IFiscalYearService fiscalYearService,
                                           DialogService dialogService,
                                           IMessenger messenger)
            : base(messenger)
        {
            _engagementService = engagementService;
            _fiscalYearService = fiscalYearService;
            _dialogService = dialogService;
        }

        public abstract string Header { get; }

        public override async Task LoadDataAsync()
        {
            Engagements = new ObservableCollection<Engagement>(await _engagementService.GetAllAsync());
            FiscalYears = new ObservableCollection<FiscalYear>(await _fiscalYearService.GetAllAsync());
        }

        [RelayCommand]
        private async Task EditAllocation(Engagement engagement)
        {
            if (engagement == null)
            {
                return;
            }

            var editorViewModel = new AllocationEditorViewModel(engagement,
                                                                FiscalYears.ToList(),
                                                                _engagementService,
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

            var editorViewModel = new AllocationEditorViewModel(engagement,
                                                                FiscalYears.ToList(),
                                                                _engagementService,
                                                                Messenger,
                                                                isReadOnlyMode: true);
            await _dialogService.ShowDialogAsync(editorViewModel);
        }
    }
}
