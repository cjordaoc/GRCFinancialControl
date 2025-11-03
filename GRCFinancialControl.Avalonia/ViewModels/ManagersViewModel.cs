using System.Collections.ObjectModel;
using System.Threading.Tasks;
using App.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRC.Shared.UI.Messages;
using GRCFinancialControl.Avalonia.Services;
using GRCFinancialControl.Avalonia.ViewModels.Dialogs;
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

        private readonly IEngagementService _engagementService;
        private readonly IPapdService _papdService;
        private readonly ICustomerService _customerService;
        private readonly IClosingPeriodService _closingPeriodService;

        public ManagersViewModel(
            IManagerService managerService,
            IEngagementService engagementService,
            IPapdService papdService,
            ICustomerService customerService,
            IClosingPeriodService closingPeriodService,
            DialogService dialogService,
            IMessenger messenger)
            : base(messenger)
        {
            _managerService = managerService;
            _engagementService = engagementService;
            _papdService = papdService;
            _customerService = customerService;
            _closingPeriodService = closingPeriodService;
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
                ToastService.ShowSuccess("FINC_Managers_Toast_DeleteSuccess", manager.Name);
                Messenger.Send(new RefreshViewMessage(RefreshTargets.FinancialData));
            }
            catch (System.Exception ex)
            {
                ToastService.ShowError("FINC_Managers_Toast_OperationFailed", ex.Message);
            }
        }

        [RelayCommand(CanExecute = nameof(CanAssignEngagements))]
        private async Task AssignEngagements()
        {
            if (SelectedManager is null)
            {
                return;
            }

            var engagement = new Engagement();
            engagement.ManagerAssignments.Add(new EngagementManagerAssignment { Manager = SelectedManager });
            var editorViewModel = new EngagementEditorViewModel(engagement, _engagementService, _customerService, _closingPeriodService, Messenger);
            await _dialogService.ShowDialogAsync(editorViewModel);
            Messenger.Send(new RefreshViewMessage(RefreshTargets.FinancialData));
        }

        private bool CanModifySelection(Manager? manager) => manager is not null;
        private bool CanAssignEngagements() => SelectedManager is not null;

        partial void OnSelectedManagerChanged(Manager? value)
        {
            EditCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
            ViewCommand.NotifyCanExecuteChanged();
            AssignEngagementsCommand.NotifyCanExecuteChanged();
        }
    }
}
