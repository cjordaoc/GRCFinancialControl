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
using GRCFinancialControl.Core.Enums;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class ManagerAssignmentsViewModel : ViewModelBase
    {
        private readonly IManagerAssignmentService _assignmentService;
        private readonly IEngagementService _engagementService;
        private readonly IManagerService _managerService;
        private readonly DialogService _dialogService;

        [ObservableProperty]
        private ObservableCollection<EngagementOption> _engagements = new();

        [ObservableProperty]
        private ObservableCollection<Manager> _managers = new();

        [ObservableProperty]
        private Manager? _selectedManager;

        [ObservableProperty]
        private ObservableCollection<ManagerAssignmentItem> _assignments = new();

        [ObservableProperty]
        private ManagerAssignmentItem? _selectedAssignment;

        public ManagerAssignmentsViewModel(
            IManagerAssignmentService assignmentService,
            IEngagementService engagementService,
            IManagerService managerService,
            DialogService dialogService,
            IMessenger messenger)
            : base(messenger)
        {
            _assignmentService = assignmentService;
            _engagementService = engagementService;
            _managerService = managerService;
            _dialogService = dialogService;
        }

        public override async Task LoadDataAsync()
        {
            var engagements = await _engagementService.GetAllAsync();
            var engagementOptions = engagements
                .OrderBy(e => e.Description, StringComparer.OrdinalIgnoreCase)
                .Select(e => new EngagementOption(e.Id, e.EngagementId, e.Description))
                .ToList();

            Engagements = new ObservableCollection<EngagementOption>(engagementOptions);

            var managers = await _managerService.GetAllAsync();
            var orderedManagers = managers
                .Where(m => m.Position is ManagerPosition.Manager or ManagerPosition.SeniorManager)
                .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var previousManagerId = SelectedManager?.Id;
            Managers = new ObservableCollection<Manager>(orderedManagers);
            if (previousManagerId.HasValue)
            {
                SelectedManager = Managers.FirstOrDefault(m => m.Id == previousManagerId.Value)
                    ?? Managers.FirstOrDefault();
            }
            else if (SelectedManager is null)
            {
                SelectedManager = Managers.FirstOrDefault();
            }

            AddCommand.NotifyCanExecuteChanged();

            await LoadAssignmentsForSelectedManagerAsync();
        }

        [RelayCommand(CanExecute = nameof(CanAddAssignment))]
        private async Task AddAsync()
        {
            if (SelectedManager is null)
            {
                return;
            }

            var assignment = new EngagementManagerAssignment
            {
                ManagerId = SelectedManager.Id,
                Manager = SelectedManager
            };

            var editorViewModel = new ManagerAssignmentEditorViewModel(
                assignment,
                Engagements,
                Managers,
                _assignmentService,
                Messenger)
            {
                SelectedManager = SelectedManager,
                SelectedEngagement = Engagements.FirstOrDefault()
            };

            await editorViewModel.LoadDataAsync();
            var result = await _dialogService.ShowDialogAsync(editorViewModel);
            if (result)
            {
                Messenger.Send(new RefreshViewMessage(RefreshTargets.FinancialData));
            }
        }

        [RelayCommand(CanExecute = nameof(CanModifySelection))]
        private async Task EditAsync()
        {
            if (SelectedAssignment is null)
            {
                return;
            }

            var existingAssignment = await _assignmentService.GetByIdAsync(SelectedAssignment.AssignmentId);
            if (existingAssignment is null)
            {
                ToastService.ShowWarning(
                    "Admin.ManagerAssignments.Toast.OperationFailed",
                    LocalizationRegistry.Get("Admin.ManagerAssignments.Error.AssignmentMissing"));
                return;
            }

            var editorViewModel = new ManagerAssignmentEditorViewModel(
                existingAssignment,
                Engagements,
                Managers,
                _assignmentService,
                Messenger)
            {
                SelectedEngagement = Engagements.FirstOrDefault(e => e.InternalId == existingAssignment.EngagementId),
                SelectedManager = Managers.FirstOrDefault(m => m.Id == existingAssignment.ManagerId)
            };

            await editorViewModel.LoadDataAsync();
            var result = await _dialogService.ShowDialogAsync(editorViewModel);
            if (result)
            {
                Messenger.Send(new RefreshViewMessage(RefreshTargets.FinancialData));
            }
        }

        [RelayCommand(CanExecute = nameof(CanModifySelection))]
        private async Task ViewAsync()
        {
            if (SelectedAssignment is null)
            {
                return;
            }

            var existingAssignment = await _assignmentService.GetByIdAsync(SelectedAssignment.AssignmentId);
            if (existingAssignment is null)
            {
                ToastService.ShowWarning(
                    "Admin.ManagerAssignments.Toast.OperationFailed",
                    LocalizationRegistry.Get("Admin.ManagerAssignments.Error.AssignmentMissing"));
                return;
            }

            var editorViewModel = new ManagerAssignmentEditorViewModel(
                existingAssignment,
                Engagements,
                Managers,
                _assignmentService,
                Messenger,
                isReadOnlyMode: true)
            {
                SelectedEngagement = Engagements.FirstOrDefault(e => e.InternalId == existingAssignment.EngagementId),
                SelectedManager = Managers.FirstOrDefault(m => m.Id == existingAssignment.ManagerId)
            };

            await editorViewModel.LoadDataAsync();
            await _dialogService.ShowDialogAsync(editorViewModel);
        }

        [RelayCommand(CanExecute = nameof(CanModifySelection))]
        private async Task DeleteAsync()
        {
            if (SelectedAssignment is null)
            {
                return;
            }

            var confirmed = await _dialogService.ShowConfirmationAsync(
                LocalizationRegistry.Get("Admin.ManagerAssignments.Dialog.Remove.Title"),
                LocalizationRegistry.Format(
                    "Admin.ManagerAssignments.Dialog.Remove.Message",
                    SelectedAssignment.ManagerName,
                    SelectedAssignment.EngagementDisplay)
            );

            if (!confirmed)
            {
                return;
            }

            try
            {
                await _assignmentService.DeleteAsync(SelectedAssignment.AssignmentId);
                ToastService.ShowSuccess(
                    "Admin.ManagerAssignments.Toast.DeleteSuccess",
                    SelectedAssignment.ManagerName,
                    SelectedAssignment.EngagementDisplay);
                Messenger.Send(new RefreshViewMessage(RefreshTargets.FinancialData));
            }
            catch (Exception ex)
            {
                ToastService.ShowError("Admin.ManagerAssignments.Toast.OperationFailed", ex.Message);
            }
        }

        private bool CanAddAssignment() => SelectedManager is not null && Engagements.Count > 0;

        private bool CanModifySelection() => SelectedAssignment is not null;

        partial void OnSelectedManagerChanged(Manager? value)
        {
            AddCommand.NotifyCanExecuteChanged();
            _ = LoadAssignmentsForSelectedManagerAsync();
        }

        partial void OnSelectedAssignmentChanged(ManagerAssignmentItem? value)
        {
            EditCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
            ViewCommand.NotifyCanExecuteChanged();
        }

        private async Task LoadAssignmentsForSelectedManagerAsync()
        {
            if (SelectedManager is null)
            {
                Assignments = new ObservableCollection<ManagerAssignmentItem>();
                return;
            }

            var assignments = await _assignmentService.GetByManagerIdAsync(SelectedManager.Id);
            var assignmentItems = assignments
                .Select(a => new ManagerAssignmentItem(
                    a.Id,
                    a.EngagementId,
                    a.Engagement.EngagementId,
                    a.Engagement.Description,
                    a.ManagerId,
                    a.Manager.Name,
                    a.Manager.Position))
                .OrderBy(a => a.EngagementDisplay, StringComparer.OrdinalIgnoreCase)
                .ToList();

            Assignments = new ObservableCollection<ManagerAssignmentItem>(assignmentItems);
            SelectedAssignment = Assignments.FirstOrDefault();
        }
    }

    public record ManagerAssignmentItem(
        int AssignmentId,
        int EngagementInternalId,
        string EngagementId,
        string EngagementDescription,
        int ManagerId,
        string ManagerName,
        ManagerPosition ManagerPosition)
    {
        public string EngagementDisplay => string.IsNullOrWhiteSpace(EngagementId)
            ? EngagementDescription
            : LocalizationRegistry.Format(
                "Admin.ManagerAssignments.Format.EngagementDisplay",
                EngagementId,
                EngagementDescription);
    }
}
