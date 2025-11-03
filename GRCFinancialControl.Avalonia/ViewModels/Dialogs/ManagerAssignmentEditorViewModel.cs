using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using App.Presentation.Localization;
using App.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;
using GRC.Shared.UI.Messages;

namespace GRCFinancialControl.Avalonia.ViewModels.Dialogs
{
    public partial class ManagerAssignmentEditorViewModel : ViewModelBase
    {
        private readonly IManagerAssignmentService _assignmentService;
        private readonly EngagementManagerAssignment _assignment;

        [ObservableProperty]
        private ObservableCollection<EngagementOption> _engagements;

        [ObservableProperty]
        private EngagementOption? _selectedEngagement;

        [ObservableProperty]
        private ObservableCollection<Manager> _managers;

        [ObservableProperty]
        private Manager? _selectedManager;

        [ObservableProperty]
        private bool _isReadOnlyMode;

        public string Title => _assignment.Id == 0
            ? LocalizationRegistry.Get("FINC_Admin_ManagerAssignments_Dialog_Add_Title")
            : LocalizationRegistry.Get("FINC_Admin_ManagerAssignments_Dialog_Edit_Title");

        public ManagerAssignmentEditorViewModel(
            EngagementManagerAssignment assignment,
            ObservableCollection<EngagementOption> engagements,
            ObservableCollection<Manager> managers,
            IManagerAssignmentService assignmentService,
            IMessenger messenger,
            bool isReadOnlyMode = false)
            : base(messenger)
        {
            _assignment = assignment;
            _assignmentService = assignmentService;
            _engagements = engagements;
            _managers = managers;

            IsReadOnlyMode = isReadOnlyMode;
        }

        public bool AllowEditing => !IsReadOnlyMode;

        public override Task LoadDataAsync()
        {
            if (_assignment.Id > 0)
            {
                SelectedEngagement ??= Engagements.FirstOrDefault(e => e.InternalId == _assignment.EngagementId);
                SelectedManager ??= Managers.FirstOrDefault(m => m.Id == _assignment.ManagerId);
            }

            return Task.CompletedTask;
        }

        [RelayCommand(CanExecute = nameof(CanSave))]
        private async Task SaveAsync()
        {
            if (SelectedEngagement is null || SelectedManager is null)
            {
                return;
            }

            _assignment.EngagementId = SelectedEngagement.InternalId;
            _assignment.ManagerId = SelectedManager.Id;

            try
            {
                if (_assignment.Id == 0)
                {
                    await _assignmentService.AddAsync(_assignment);
                }
                else
                {
                    await _assignmentService.UpdateAsync(_assignment);
                }

                ToastService.ShowSuccess(
                    "FINC_Admin_ManagerAssignments_Toast_SaveSuccess",
                    SelectedManager.Name,
                    SelectedEngagement.ToString());

                Messenger.Send(new CloseDialogMessage(true));
            }
            catch (Exception ex)
            {
                ToastService.ShowError("FINC_Admin_ManagerAssignments_Toast_OperationFailed", ex.Message);
            }
        }

        [RelayCommand]
        private void Close()
        {
            Messenger.Send(new CloseDialogMessage(false));
        }

        private bool CanSave() => SelectedEngagement is not null && SelectedManager is not null && !IsReadOnlyMode;

        partial void OnSelectedEngagementChanged(EngagementOption? value)
        {
            SaveCommand.NotifyCanExecuteChanged();
        }

        partial void OnSelectedManagerChanged(Manager? value)
        {
            SaveCommand.NotifyCanExecuteChanged();
        }

        partial void OnIsReadOnlyModeChanged(bool value)
        {
            SaveCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(AllowEditing));
        }
    }
}
