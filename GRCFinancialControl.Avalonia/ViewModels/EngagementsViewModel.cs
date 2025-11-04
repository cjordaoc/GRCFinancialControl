using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using App.Presentation.Localization;
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
    public partial class EngagementsViewModel : ViewModelBase
    {
        private readonly IEngagementService _engagementService;
        private readonly ICustomerService _customerService;
        private readonly IClosingPeriodService _closingPeriodService;
        private readonly IPapdService _papdService;
        private readonly IManagerService _managerService;
        private readonly IManagerAssignmentService _managerAssignmentService;
        private readonly DialogService _dialogService;

        [ObservableProperty]
        private Engagement? _selectedEngagement;

        public EngagementsViewModel(
            IEngagementService engagementService, 
            ICustomerService customerService, 
            IClosingPeriodService closingPeriodService,
            IPapdService papdService,
            IManagerService managerService,
            IManagerAssignmentService managerAssignmentService,
            DialogService dialogService, 
            IMessenger messenger)
            : base(messenger)
        {
            _engagementService = engagementService;
            _customerService = customerService;
            _closingPeriodService = closingPeriodService;
            _papdService = papdService;
            _managerService = managerService;
            _managerAssignmentService = managerAssignmentService;
            _dialogService = dialogService;
        }

        [ObservableProperty]
        private ObservableCollection<Engagement> _engagements = new();

        public override async Task LoadDataAsync()
        {
            Engagements = new ObservableCollection<Engagement>(await _engagementService.GetAllAsync());
        }

        [RelayCommand]
        private async Task Add()
        {
            var editorViewModel = new EngagementEditorViewModel(new Engagement(), _engagementService, _customerService, _closingPeriodService, Messenger);
            await _dialogService.ShowDialogAsync(editorViewModel);
            Messenger.Send(new RefreshViewMessage(RefreshTargets.FinancialData));
        }

        [RelayCommand(CanExecute = nameof(CanEdit))]
        private async Task Edit(Engagement engagement)
        {
            if (engagement == null) return;
            var fullEngagement = await _engagementService.GetByIdAsync(engagement.Id);
            if (fullEngagement is null)
            {
                return;
            }

            var editorViewModel = new EngagementEditorViewModel(fullEngagement, _engagementService, _customerService, _closingPeriodService, Messenger);
            await _dialogService.ShowDialogAsync(editorViewModel);
            Messenger.Send(new RefreshViewMessage(RefreshTargets.FinancialData));
        }

        [RelayCommand(CanExecute = nameof(CanView))]
        private async Task View(Engagement engagement)
        {
            if (engagement == null) return;

            var fullEngagement = await _engagementService.GetByIdAsync(engagement.Id);
            if (fullEngagement is null)
            {
                return;
            }

            var editorViewModel = new EngagementEditorViewModel(fullEngagement, _engagementService, _customerService, _closingPeriodService, Messenger, isReadOnlyMode: true);
            await _dialogService.ShowDialogAsync(editorViewModel);
        }

        [RelayCommand(CanExecute = nameof(CanDelete))]
        private async Task Delete(Engagement engagement)
        {
            if (engagement == null) return;
            try
            {
                await _engagementService.DeleteAsync(engagement.Id);
                ToastService.ShowSuccess("FINC_Engagements_Toast_DeleteSuccess", engagement.EngagementId);
                Messenger.Send(new RefreshViewMessage(RefreshTargets.FinancialData));
            }
            catch (InvalidOperationException ex)
            {
                ToastService.ShowWarning("FINC_Engagements_Toast_OperationFailed", ex.Message);
            }
            catch (Exception ex)
            {
                ToastService.ShowError("FINC_Engagements_Toast_OperationFailed", ex.Message);
            }
        }

        [RelayCommand(CanExecute = nameof(CanDeleteData))]
        private async Task DeleteData(Engagement engagement)
        {
            if (engagement is null) return;

            var result = await _dialogService.ShowConfirmationAsync(
                LocalizationRegistry.Get("FINC_Dialog_DeleteData_Title"),
                LocalizationRegistry.Format("FINC_Dialog_DeleteData_Message", engagement.EngagementId));
            if (result)
            {
                try
                {
                    await _engagementService.DeleteDataAsync(engagement.Id);
                    ToastService.ShowSuccess("FINC_Engagements_Toast_ReverseSuccess", engagement.EngagementId);
                    Messenger.Send(new RefreshViewMessage(RefreshTargets.FinancialData));
                }
                catch (InvalidOperationException ex)
                {
                    ToastService.ShowWarning("FINC_Engagements_Toast_OperationFailed", ex.Message);
                }
                catch (Exception ex)
                {
                    ToastService.ShowError("FINC_Engagements_Toast_OperationFailed", ex.Message);
                }
            }
        }

        [RelayCommand(CanExecute = nameof(CanAssign))]
        private async Task AssignPapd()
        {
            if (SelectedEngagement is null)
            {
                return;
            }

            var fullEngagement = await _engagementService.GetByIdAsync(SelectedEngagement.Id);
            if (fullEngagement is null)
            {
                return;
            }

            var papds = await _papdService.GetAllAsync();
            if (papds.Count == 0)
            {
                ToastService.ShowWarning("FINC_Engagements_Toast_NoPapdsAvailable");
                return;
            }

            // Check if all PAPDs are already assigned
            var assignedPapdIds = fullEngagement.EngagementPapds.Select(a => a.PapdId).ToHashSet();
            var availablePapds = papds.Where(p => !assignedPapdIds.Contains(p.Id)).ToList();
            
            if (availablePapds.Count == 0)
            {
                ToastService.ShowWarning("FINC_Engagements_Toast_AllPapdsAssigned");
                return;
            }

            // Use the selection view model for choosing a PAPD
            var selectionViewModel = new PapdSelectionViewModel(
                fullEngagement,
                _engagementService,
                _papdService,
                Messenger);

            await selectionViewModel.LoadDataAsync();
            await _dialogService.ShowDialogAsync(selectionViewModel, selectionViewModel.Title);
            Messenger.Send(new RefreshViewMessage(RefreshTargets.FinancialData));
        }

        [RelayCommand(CanExecute = nameof(CanAssign))]
        private async Task AssignManager()
        {
            if (SelectedEngagement is null)
            {
                return;
            }

            var fullEngagement = await _engagementService.GetByIdAsync(SelectedEngagement.Id);
            if (fullEngagement is null)
            {
                return;
            }

            var managers = await _managerService.GetAllAsync();
            var assignedManagerIds = fullEngagement.ManagerAssignments.Select(a => a.ManagerId).ToHashSet();
            var availableManagers = managers.Where(m => !assignedManagerIds.Contains(m.Id)).ToList();
            
            if (availableManagers.Count == 0)
            {
                ToastService.ShowWarning("FINC_Engagements_Toast_AllManagersAssigned");
                return;
            }

            var selectionViewModel = new ManagerSelectionViewModel(
                fullEngagement,
                _engagementService,
                _managerService,
                Messenger);

            await selectionViewModel.LoadDataAsync();
            await _dialogService.ShowDialogAsync(selectionViewModel, selectionViewModel.Title);
            Messenger.Send(new RefreshViewMessage(RefreshTargets.FinancialData));
        }

        private static bool CanEdit(Engagement engagement) => engagement is not null;

        private static bool CanView(Engagement engagement) => engagement is not null;

        private static bool CanDelete(Engagement engagement) => engagement is not null;

        private static bool CanDeleteData(Engagement engagement) => engagement is not null;

        private bool CanAssign() => SelectedEngagement is not null;

        partial void OnSelectedEngagementChanged(Engagement? value)
        {
            EditCommand.NotifyCanExecuteChanged();
            ViewCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
            DeleteDataCommand.NotifyCanExecuteChanged();
            AssignPapdCommand.NotifyCanExecuteChanged();
            AssignManagerCommand.NotifyCanExecuteChanged();
        }

    }
}
