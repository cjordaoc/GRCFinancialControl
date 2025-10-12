using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class FiscalYearsViewModel : ViewModelBase
    {
        private readonly IFiscalYearService _fiscalYearService;
        private readonly IDialogService _dialogService;

        [ObservableProperty]
        private ObservableCollection<FiscalYear> _fiscalYears = new();

        public FiscalYearsViewModel(IFiscalYearService fiscalYearService, IDialogService dialogService, IMessenger messenger)
            : base(messenger)
        {
            _fiscalYearService = fiscalYearService;
            _dialogService = dialogService;
        }

        public override async Task LoadDataAsync()
        {
            FiscalYears = new ObservableCollection<FiscalYear>(await _fiscalYearService.GetAllAsync());
        }

        [RelayCommand]
        private async Task Add()
        {
            var editorViewModel = new FiscalYearEditorViewModel(new FiscalYear(), _fiscalYearService, Messenger);
            await _dialogService.ShowDialogAsync(editorViewModel);
            Messenger.Send(new RefreshDataMessage());
        }

        [RelayCommand]
        private async Task Edit(FiscalYear fiscalYear)
        {
            if (fiscalYear == null) return;
            var editorViewModel = new FiscalYearEditorViewModel(fiscalYear, _fiscalYearService, Messenger);
            await _dialogService.ShowDialogAsync(editorViewModel);
            Messenger.Send(new RefreshDataMessage());
        }

        [RelayCommand]
        private async Task Delete(FiscalYear fiscalYear)
        {
            if (fiscalYear == null) return;
            await _fiscalYearService.DeleteAsync(fiscalYear.Id);
            Messenger.Send(new RefreshDataMessage());
        }
    }
}
