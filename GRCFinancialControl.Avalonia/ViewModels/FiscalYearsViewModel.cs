using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;
using GRCFinancialControl.Avalonia.Services.Interfaces;
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

        [ObservableProperty]
        private FiscalYear? _selectedFiscalYear;

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

        [RelayCommand(CanExecute = nameof(CanDeleteData))]
        private async Task DeleteData()
        {
            if (SelectedFiscalYear is null) return;

            var result = await _dialogService.ShowConfirmationAsync("Delete Data", $"Are you sure you want to delete all data for {SelectedFiscalYear.Name}? This action cannot be undone.");
            if (result)
            {
                await _fiscalYearService.DeleteDataAsync(SelectedFiscalYear.Id);
                Messenger.Send(new RefreshDataMessage());
            }
        }

        private bool CanDeleteData() => SelectedFiscalYear is not null;

        partial void OnSelectedFiscalYearChanged(FiscalYear? value)
        {
            EditCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
            DeleteDataCommand.NotifyCanExecuteChanged();
        }
    }
}
