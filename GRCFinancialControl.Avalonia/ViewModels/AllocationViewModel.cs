using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Services.Interfaces;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class AllocationViewModel : ViewModelBase
    {
        private readonly IEngagementService _engagementService;
        private readonly IFiscalYearService _fiscalYearService;
        private readonly IDialogService _dialogService;

        [ObservableProperty]
        private ObservableCollection<Engagement> _engagements = new();

        [ObservableProperty]
        private ObservableCollection<FiscalYear> _fiscalYears = new();

        [ObservableProperty]
        private Engagement? _selectedEngagement;

        public AllocationViewModel(IEngagementService engagementService, IFiscalYearService fiscalYearService, IDialogService dialogService, IMessenger messenger)
            : base(messenger)
        {
            _engagementService = engagementService;
            _fiscalYearService = fiscalYearService;
            _dialogService = dialogService;
        }

        public override async Task LoadDataAsync()
        {
            Engagements = new ObservableCollection<Engagement>(await _engagementService.GetAllAsync());
            FiscalYears = new ObservableCollection<FiscalYear>(await _fiscalYearService.GetAllAsync());
        }

        [RelayCommand]
        private async Task EditAllocation(Engagement engagement)
        {
            if (engagement == null) return;

            var editorViewModel = new AllocationEditorViewModel(engagement, FiscalYears.ToList(), _engagementService, Messenger);
            await _dialogService.ShowDialogAsync(editorViewModel);
            Messenger.Send(new Messages.RefreshDataMessage());
        }
    }
}