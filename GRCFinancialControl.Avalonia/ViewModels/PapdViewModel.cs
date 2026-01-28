using System;
using System.Collections.Generic;
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
    public partial class PapdViewModel : ViewModelBase
    {
        private readonly IPapdService _papdService;
        private readonly DialogService _dialogService;

        [ObservableProperty]
        private Papd? _selectedPapd;

        private readonly IEngagementService _engagementService;
        private readonly IManagerService _managerService;
        private readonly ICustomerService _customerService;
        private readonly IClosingPeriodService _closingPeriodService;
        private readonly IPapdAssignmentService _papdAssignmentService;

        public PapdViewModel(
            IPapdService papdService,
            IEngagementService engagementService,
            IManagerService managerService,
            ICustomerService customerService,
            IClosingPeriodService closingPeriodService,
            IPapdAssignmentService papdAssignmentService,
            DialogService dialogService,
            IMessenger messenger)
            : base(messenger)
        {
            _papdService = papdService;
            _engagementService = engagementService;
            _managerService = managerService;
            _customerService = customerService;
            _closingPeriodService = closingPeriodService;
            _papdAssignmentService = papdAssignmentService;
            _dialogService = dialogService;
        }

        [ObservableProperty]
        private ObservableCollection<Papd> _papds = new();

        public override async Task LoadDataAsync()
        {
            try
            {
                Papds = new ObservableCollection<Papd>(await _papdService.GetAllAsync());
            }
            catch (Exception ex)
            {
                Papds = new ObservableCollection<Papd>();
                ToastService.ShowError("FINC_Papds_Toast_LoadError", ex.Message);
                throw;
            }
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
            if (papd == null)
            {
                return;
            }

            var editorViewModel = new PapdEditorViewModel(papd, _papdService, Messenger);
            await _dialogService.ShowDialogAsync(editorViewModel);
            Messenger.Send(new RefreshViewMessage(RefreshTargets.FinancialData));
        }

        [RelayCommand(CanExecute = nameof(CanEdit))]
        private async Task View(Papd papd)
        {
            if (papd == null)
            {
                return;
            }

            var editorViewModel = new PapdEditorViewModel(papd, _papdService, Messenger, isReadOnlyMode: true);
            await _dialogService.ShowDialogAsync(editorViewModel);
        }

        [RelayCommand(CanExecute = nameof(CanDelete))]
        private async Task Delete(Papd papd)
        {
            if (papd == null)
            {
                return;
            }

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
            if (papd is null)
            {
                return;
            }

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

        [RelayCommand(CanExecute = nameof(CanEditAssignment))]
        private async Task EditAssignment()
        {
            if (SelectedPapd is null)
            {
                return;
            }

            var editViewModel = new EditAssignmentViewModel(SelectedPapd, _papdAssignmentService, Messenger);
            await editViewModel.LoadDataAsync();
            await _dialogService.ShowDialogAsync(editViewModel, editViewModel.Title);
        }

        [RelayCommand(CanExecute = nameof(CanEditAssignment))]
        private async Task TransferAssignments()
        {
            if (SelectedPapd is null)
            {
                return;
            }

            try
            {
                // Get all assignments for this PAPD
                var assignments = await _papdAssignmentService.GetByPapdIdAsync(SelectedPapd.Id);
                if (assignments.Count == 0)
                {
                    ToastService.ShowWarning("FINC_Papds_Toast_NoAssignments");
                    return;
                }

                // Get all PAPDs except the current one
                var allPapds = await _papdService.GetAllAsync();
                var availablePapds = allPapds.Where(p => p.Id != SelectedPapd.Id).ToList();
                
                if (availablePapds.Count == 0)
                {
                    ToastService.ShowWarning("FINC_Papds_Toast_NoPapdsForTransfer");
                    return;
                }

                // Create a simple dialog to select target PAPD
                var transferViewModel = new PapdTransferViewModel(availablePapds, Messenger);
                await _dialogService.ShowDialogAsync(transferViewModel, "Transfer Assignments");

                // If transfer was confirmed, transfer assignments
                if (transferViewModel.SelectedPapd != null)
                {
                    foreach (var assignment in assignments)
                    {
                        // Update the assignment to point to the new PAPD
                        assignment.PapdId = transferViewModel.SelectedPapd.Id;
                    }
                    
                    // Now remove the original assignments
                    foreach (var assignment in assignments)
                    {
                        await _papdAssignmentService.DeleteAsync(assignment.Id);
                    }

                    ToastService.ShowSuccess("FINC_Papds_Toast_TransferSuccess", 
                        SelectedPapd.Name, 
                        transferViewModel.SelectedPapd.Name,
                        assignments.Count.ToString());
                    
                    Messenger.Send(new RefreshViewMessage(RefreshTargets.FinancialData));
                }
            }
            catch (System.Exception ex)
            {
                ToastService.ShowError("FINC_Papds_Toast_OperationFailed", ex.Message);
            }
        }

        [RelayCommand(CanExecute = nameof(CanEditAssignment))]
        private async Task DeleteAllAssignments()
        {
            if (SelectedPapd is null)
            {
                return;
            }

            try
            {
                // Get all assignments for this PAPD
                var assignments = await _papdAssignmentService.GetByPapdIdAsync(SelectedPapd.Id);
                if (assignments.Count == 0)
                {
                    ToastService.ShowWarning("FINC_Papds_Toast_NoAssignments");
                    return;
                }

                // Confirm deletion
                var result = await _dialogService.ShowConfirmationAsync(
                    "Delete All Assignments",
                    $"Are you sure you want to delete all {assignments.Count} assignment(s) for {SelectedPapd.Name}?");
                
                if (!result)
                {
                    return;
                }

                // Delete all assignments
                foreach (var assignment in assignments)
                {
                    await _papdAssignmentService.DeleteAsync(assignment.Id);
                }

                ToastService.ShowSuccess("FINC_Papds_Toast_DeleteAssignmentsSuccess", 
                    assignments.Count.ToString(), 
                    SelectedPapd.Name);
                
                Messenger.Send(new RefreshViewMessage(RefreshTargets.FinancialData));
            }
            catch (System.Exception ex)
            {
                ToastService.ShowError("FINC_Papds_Toast_OperationFailed", ex.Message);
            }
        }

        private static bool CanEdit(Papd papd) => papd is not null;

        private static bool CanDelete(Papd papd) => papd is not null;

        private static bool CanDeleteData(Papd papd) => papd is not null;

        private bool CanEditAssignment() => SelectedPapd is not null;

        partial void OnSelectedPapdChanged(Papd? value)
        {
            EditCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
            DeleteDataCommand.NotifyCanExecuteChanged();
            EditAssignmentCommand.NotifyCanExecuteChanged();
            ViewCommand.NotifyCanExecuteChanged();
            TransferAssignmentsCommand.NotifyCanExecuteChanged();
            DeleteAllAssignmentsCommand.NotifyCanExecuteChanged();
        }

    }
}