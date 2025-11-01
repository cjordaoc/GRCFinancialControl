using System.Collections.ObjectModel;
using System.Threading.Tasks;
using App.Presentation.Localization;
using App.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;
using GRCFinancialControl.Avalonia.Services;
using GRCFinancialControl.Avalonia.ViewModels.Dialogs;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class PapdViewModel : ViewModelBase
    {
        private readonly IPapdService _papdService;
        private readonly DialogService _dialogService;
        private readonly IEngagementService _engagementService;

        [ObservableProperty]
        private Papd? _selectedPapd;

        public PapdViewModel(IPapdService papdService, DialogService dialogService, IEngagementService engagementService, IMessenger messenger)
            : base(messenger)
        {
            _papdService = papdService;
            _dialogService = dialogService;
            _engagementService = engagementService;
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
            Messenger.Send(new RefreshDataMessage());
        }

        [RelayCommand(CanExecute = nameof(CanEdit))]
        private async Task Edit(Papd papd)
        {
            if (papd == null) return;
            var editorViewModel = new PapdEditorViewModel(papd, _papdService, Messenger);
            await _dialogService.ShowDialogAsync(editorViewModel);
            Messenger.Send(new RefreshDataMessage());
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
                ToastService.ShowSuccess("Papds.Toast.DeleteSuccess", papd.Name);
                Messenger.Send(new RefreshDataMessage());
            }
            catch (System.Exception ex)
            {
                ToastService.ShowError("Papds.Toast.OperationFailed", ex.Message);
            }
        }

        [RelayCommand(CanExecute = nameof(CanDeleteData))]
        private async Task DeleteData(Papd papd)
        {
            if (papd is null) return;

            var result = await _dialogService.ShowConfirmationAsync(
                LocalizationRegistry.Get("Common.Dialog.DeleteData.Title"),
                LocalizationRegistry.Format("Common.Dialog.DeleteData.Message", papd.Name));
            if (result)
            {
                try
                {
                    await _papdService.DeleteDataAsync(papd.Id);
                    ToastService.ShowSuccess("Papds.Toast.DeleteDataSuccess", papd.Name);
                    Messenger.Send(new RefreshDataMessage());
                }
                catch (System.Exception ex)
                {
                    ToastService.ShowError("Papds.Toast.OperationFailed", ex.Message);
                }
            }
        }

        [RelayCommand(CanExecute = nameof(CanManageAssignments))]
        private async Task ManageAssignmentsAsync()
        {
            if (SelectedPapd is null)
            {
                return;
            }

            var assignmentViewModel = new PapdEngagementAssignmentViewModel(
                SelectedPapd,
                new EngagementPapd { PapdId = SelectedPapd.Id, Papd = SelectedPapd },
                _engagementService,
                Messenger);
            await assignmentViewModel.LoadDataAsync();
            await _dialogService.ShowDialogAsync(assignmentViewModel);
        }

        private static bool CanEdit(Papd papd) => papd is not null;

        private static bool CanDelete(Papd papd) => papd is not null;

        private static bool CanDeleteData(Papd papd) => papd is not null;

        private bool CanManageAssignments() => SelectedPapd is not null;

        partial void OnSelectedPapdChanged(Papd? value)
        {
            EditCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
            DeleteDataCommand.NotifyCanExecuteChanged();
            ManageAssignmentsCommand.NotifyCanExecuteChanged();
            ViewCommand.NotifyCanExecuteChanged();
        }

    }
}