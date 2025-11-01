using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using App.Presentation.Localization;
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
    public partial class EngagementsViewModel : ViewModelBase
    {
        private readonly IEngagementService _engagementService;
        private readonly ICustomerService _customerService;
        private readonly IClosingPeriodService _closingPeriodService;
        private readonly DialogService _dialogService;

        [ObservableProperty]
        private Engagement? _selectedEngagement;

        public EngagementsViewModel(IEngagementService engagementService, ICustomerService customerService, IClosingPeriodService closingPeriodService, DialogService dialogService, IMessenger messenger)
            : base(messenger)
        {
            _engagementService = engagementService;
            _customerService = customerService;
            _closingPeriodService = closingPeriodService;
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
                ToastService.ShowSuccess("Engagements.Toast.DeleteSuccess", engagement.EngagementId);
                Messenger.Send(new RefreshViewMessage(RefreshTargets.FinancialData));
            }
            catch (InvalidOperationException ex)
            {
                ToastService.ShowWarning("Engagements.Toast.OperationFailed", ex.Message);
            }
            catch (Exception ex)
            {
                ToastService.ShowError("Engagements.Toast.OperationFailed", ex.Message);
            }
        }

        [RelayCommand(CanExecute = nameof(CanDeleteData))]
        private async Task DeleteData(Engagement engagement)
        {
            if (engagement is null) return;

            var result = await _dialogService.ShowConfirmationAsync(
                LocalizationRegistry.Get("Common.Dialog.DeleteData.Title"),
                LocalizationRegistry.Format("Common.Dialog.DeleteData.Message", engagement.EngagementId));
            if (result)
            {
                try
                {
                    await _engagementService.DeleteDataAsync(engagement.Id);
                    ToastService.ShowSuccess("Engagements.Toast.ReverseSuccess", engagement.EngagementId);
                    Messenger.Send(new RefreshViewMessage(RefreshTargets.FinancialData));
                }
                catch (InvalidOperationException ex)
                {
                    ToastService.ShowWarning("Engagements.Toast.OperationFailed", ex.Message);
                }
                catch (Exception ex)
                {
                    ToastService.ShowError("Engagements.Toast.OperationFailed", ex.Message);
                }
            }
        }

        private static bool CanEdit(Engagement engagement) => engagement is not null;

        private static bool CanView(Engagement engagement) => engagement is not null;

        private static bool CanDelete(Engagement engagement) => engagement is not null;

        private static bool CanDeleteData(Engagement engagement) => engagement is not null;

        partial void OnSelectedEngagementChanged(Engagement? value)
        {
            EditCommand.NotifyCanExecuteChanged();
            ViewCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
            DeleteDataCommand.NotifyCanExecuteChanged();
        }

    }
}
