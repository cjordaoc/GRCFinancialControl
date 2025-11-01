using System.Collections.ObjectModel;
using System.Threading.Tasks;
using App.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRC.Shared.UI.Messages;
using GRCFinancialControl.Avalonia.Services;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class ManagersViewModel : ViewModelBase
    {
        private readonly IManagerService _managerService;
        private readonly DialogService _dialogService;

        [ObservableProperty]
        private ObservableCollection<Manager> _managers = new();

        [ObservableProperty]
        private Manager? _selectedManager;

        public ManagersViewModel(IManagerService managerService, DialogService dialogService, IMessenger messenger)
            : base(messenger)
        {
            _managerService = managerService;
            _dialogService = dialogService;
        }

        public override async Task LoadDataAsync()
        {
            Managers = new ObservableCollection<Manager>(await _managerService.GetAllAsync());
        }

        [RelayCommand]
        private async Task Add()
        {
            var editorViewModel = new ManagerEditorViewModel(new Manager(), _managerService, Messenger);
            await _dialogService.ShowDialogAsync(editorViewModel);
            Messenger.Send(new RefreshViewMessage(RefreshTargets.FinancialData));
        }

        [RelayCommand(CanExecute = nameof(CanModifySelection))]
        private async Task Edit(Manager manager)
        {
            if (manager is null)
            {
                return;
            }

            var editorViewModel = new ManagerEditorViewModel(manager, _managerService, Messenger);
            await _dialogService.ShowDialogAsync(editorViewModel);
            Messenger.Send(new RefreshViewMessage(RefreshTargets.FinancialData));
        }

        [RelayCommand(CanExecute = nameof(CanModifySelection))]
        private async Task View(Manager manager)
        {
            if (manager is null)
            {
                return;
            }

            var editorViewModel = new ManagerEditorViewModel(manager, _managerService, Messenger, isReadOnlyMode: true);
            await _dialogService.ShowDialogAsync(editorViewModel);
        }

        [RelayCommand(CanExecute = nameof(CanModifySelection))]
        private async Task Delete(Manager manager)
        {
            if (manager is null)
            {
                return;
            }

            try
            {
                await _managerService.DeleteAsync(manager.Id);
                ToastService.ShowSuccess("Managers.Toast.DeleteSuccess", manager.Name);
                Messenger.Send(new RefreshViewMessage(RefreshTargets.FinancialData));
            }
            catch (System.Exception ex)
            {
                ToastService.ShowError("Managers.Toast.OperationFailed", ex.Message);
            }
        }

        private bool CanModifySelection(Manager? manager) => manager is not null;

        partial void OnSelectedManagerChanged(Manager? value)
        {
            EditCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
            ViewCommand.NotifyCanExecuteChanged();
        }
    }
}
