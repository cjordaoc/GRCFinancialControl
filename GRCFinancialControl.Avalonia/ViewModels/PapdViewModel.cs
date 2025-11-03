using System.Collections.ObjectModel;
using System.Threading.Tasks;
using App.Presentation.Localization;
using App.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRC.Shared.UI.Messages;
using GRCFinancialControl.Avalonia.Services;
using GRCFinancialControl.Avalonia.ViewModels.Dialogs;
using System;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class PapdViewModel : ViewModelBase
    {
        private readonly IPapdService _papdService;
        private readonly DialogService _dialogService;
        private readonly Func<PapdEngagementAssignmentViewModel> _engagementAssignmentViewModelFactory;

        [ObservableProperty]
        private Papd? _selectedPapd;

        private readonly IEngagementService _engagementService;
        private readonly IManagerService _managerService;
        private readonly ICustomerService _customerService;
        private readonly IClosingPeriodService _closingPeriodService;

        public PapdViewModel(
            IPapdService papdService,
            IEngagementService engagementService,
            IManagerService managerService,
            ICustomerService customerService,
            IClosingPeriodService closingPeriodService,
            DialogService dialogService,
            Func<PapdEngagementAssignmentViewModel> engagementAssignmentViewModelFactory,
            IMessenger messenger)
            : base(messenger)
        {
            _papdService = papdService;
            _engagementService = engagementService;
            _managerService = managerService;
            _customerService = customerService;
            _closingPeriodService = closingPeriodService;
            _dialogService = dialogService;
            _engagementAssignmentViewModelFactory = engagementAssignmentViewModelFactory;
        }

        [ObservableProperty]
        private ObservableCollection<Papd> _papds = new();

        public override async Task LoadDataAsync()
        {
            Papds = new ObservableCollection<Papd>(await _papdService.GetAllAsync());
        }

        [RelayCommand]
        private async Task Add()
        {
            var editorViewModel = new PapdEditorViewModel(new Papd(), _papdService, Messenger);
            await _dialogService.ShowDialogAsync(editorViewModel);
            Messenger.Send(new RefreshViewMessage(RefreshTargets.FinancialData));
        }

        [RelayCommand(CanExecute = nameof(CanEdit))]
        private async Task Edit(Papd papd)
        {
            if (papd == null) return;
            var editorViewModel = new PapdEditorViewModel(papd, _papdService, Messenger);
            await _dialogService.ShowDialogAsync(editorViewModel);
            Messenger.Send(new RefreshViewMessage(RefreshTargets.FinancialData));
        }

        [RelayCommand(CanExecute = nameof(CanEdit))]
        private async Task View(Papd papd)
        {
            if (papd == null) return;
            var editorViewModel = new PapdEditorViewModel(papd, _papdService, Messenger, isReadOnlyMode: true);
            await _dialogService.ShowDialogAsync(editorViewModel);
        }

        [RelayCommand(CanExecute = nameof(CanDelete))]
        private async Task Delete(Papd papd)
        {
            if (papd == null) return;
            try
            {
                await _papdService.DeleteAsync(papd.Id);
                ToastService.ShowSuccess("FINC_Papds_Toast_DeleteSuccess", papd.Name);
                Messenger.Send(new RefreshViewMessage(RefreshTargets.FinancialData));
            }
            catch (System.Exception ex)
            {
                ToastService.ShowError("FINC_Papds_Toast_OperationFailed", ex.Message);
            }
        }

        [RelayCommand(CanExecute = nameof(CanDeleteData))]
        private async Task DeleteData(Papd papd)
        {
            if (papd is null) return;

            var result = await _dialogService.ShowConfirmationAsync(
                LocalizationRegistry.Get("FINC_Dialog_DeleteData_Title"),
                LocalizationRegistry.Format("FINC_Dialog_DeleteData_Message", papd.Name));
            if (result)
            {
                try
                {
                    await _papdService.DeleteDataAsync(papd.Id);
                    ToastService.ShowSuccess("FINC_Papds_Toast_DeleteDataSuccess", papd.Name);
                    Messenger.Send(new RefreshViewMessage(RefreshTargets.FinancialData));
                }
                catch (System.Exception ex)
                {
                    ToastService.ShowError("FINC_Papds_Toast_OperationFailed", ex.Message);
                }
            }
        }

        [RelayCommand(CanExecute = nameof(CanAssignEngagements))]
        private async Task AssignEngagements()
        {
            if (SelectedPapd is null)
            {
                return;
            }

            var engagement = new Engagement();
            engagement.EngagementPapds.Add(new EngagementPapd { Papd = SelectedPapd });
            var editorViewModel = new EngagementEditorViewModel(engagement, _engagementService, _customerService, _closingPeriodService, Messenger);
            await _dialogService.ShowDialogAsync(editorViewModel);
            Messenger.Send(new RefreshViewMessage(RefreshTargets.FinancialData));
        }

        private static bool CanEdit(Papd papd) => papd is not null;

        private static bool CanDelete(Papd papd) => papd is not null;

        private static bool CanDeleteData(Papd papd) => papd is not null;

        private bool CanAssignEngagements() => SelectedPapd is not null;

        partial void OnSelectedPapdChanged(Papd? value)
        {
            EditCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
            DeleteDataCommand.NotifyCanExecuteChanged();
            AssignEngagementsCommand.NotifyCanExecuteChanged();
            ViewCommand.NotifyCanExecuteChanged();
        }

    }
}