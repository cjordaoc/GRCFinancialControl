using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using App.Presentation.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;
using GRCFinancialControl.Avalonia.Services.Interfaces;
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
        private readonly IDialogService _dialogService;

        [ObservableProperty]
        private ObservableCollection<EngagementOption> _engagements = new();

        [ObservableProperty]
        private EngagementOption? _selectedEngagement;

        [ObservableProperty]
        private ObservableCollection<ManagerAssignmentItem> _assignments = new();

        [ObservableProperty]
        private ManagerAssignmentItem? _selectedAssignment;

        private ObservableCollection<Manager> _managers = new();

        public ManagerAssignmentsViewModel(
            IManagerAssignmentService assignmentService,
            IEngagementService engagementService,
            IManagerService managerService,
            IDialogService dialogService,
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

            var previousSelectionId = SelectedEngagement?.InternalId;
            Engagements = new ObservableCollection<EngagementOption>(engagementOptions);
            if (previousSelectionId.HasValue)
            {
                SelectedEngagement = Engagements.FirstOrDefault(e => e.InternalId == previousSelectionId.Value)
                    ?? Engagements.FirstOrDefault();
            }
            else if (SelectedEngagement is null)
            {
                SelectedEngagement = Engagements.FirstOrDefault();
            }

            var managers = await _managerService.GetAllAsync();
            _managers = new ObservableCollection<Manager>(managers
                .Where(m => m.Position is ManagerPosition.Manager or ManagerPosition.SeniorManager)
                .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase));

            AddCommand.NotifyCanExecuteChanged();

            await LoadAssignmentsForSelectedEngagementAsync();
        }

        [RelayCommand(CanExecute = nameof(CanAddAssignment))]
        private async Task AddAsync()
        {
            if (SelectedEngagement is null)
            {
                return;
            }

            var assignment = new EngagementManagerAssignment
            {
                EngagementId = SelectedEngagement.InternalId,
                BeginDate = DateTime.Today
            };

            var editorViewModel = new ManagerAssignmentEditorViewModel(
                assignment,
                Engagements,
                _managers,
                _assignmentService,
                Messenger)
            {
                SelectedEngagement = SelectedEngagement,
                SelectedManager = _managers.FirstOrDefault()
            };

            await editorViewModel.LoadDataAsync();
            var result = await _dialogService.ShowDialogAsync(editorViewModel);
            if (result)
            {
                Messenger.Send(new RefreshDataMessage());
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
                return;
            }

            var editorViewModel = new ManagerAssignmentEditorViewModel(
                existingAssignment,
                Engagements,
                _managers,
                _assignmentService,
                Messenger)
            {
                SelectedEngagement = Engagements.FirstOrDefault(e => e.InternalId == existingAssignment.EngagementId),
                SelectedManager = _managers.FirstOrDefault(m => m.Id == existingAssignment.ManagerId)
            };

            await editorViewModel.LoadDataAsync();
            var result = await _dialogService.ShowDialogAsync(editorViewModel);
            if (result)
            {
                Messenger.Send(new RefreshDataMessage());
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
                return;
            }

            var editorViewModel = new ManagerAssignmentEditorViewModel(
                existingAssignment,
                Engagements,
                _managers,
                _assignmentService,
                Messenger,
                isReadOnlyMode: true)
            {
                SelectedEngagement = Engagements.FirstOrDefault(e => e.InternalId == existingAssignment.EngagementId),
                SelectedManager = _managers.FirstOrDefault(m => m.Id == existingAssignment.ManagerId)
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

            await _assignmentService.DeleteAsync(SelectedAssignment.AssignmentId);
            Messenger.Send(new RefreshDataMessage());
        }

        private bool CanAddAssignment() => SelectedEngagement is not null && _managers.Count > 0;

        private bool CanModifySelection() => SelectedAssignment is not null;

        partial void OnSelectedEngagementChanged(EngagementOption? value)
        {
            AddCommand.NotifyCanExecuteChanged();
            _ = LoadAssignmentsForSelectedEngagementAsync();
        }

        partial void OnSelectedAssignmentChanged(ManagerAssignmentItem? value)
        {
            EditCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
            ViewCommand.NotifyCanExecuteChanged();
        }

        private async Task LoadAssignmentsForSelectedEngagementAsync()
        {
            if (SelectedEngagement is null)
            {
                Assignments = new ObservableCollection<ManagerAssignmentItem>();
                return;
            }

            var assignments = await _assignmentService.GetByEngagementIdAsync(SelectedEngagement.InternalId);
            var assignmentItems = assignments
                .Select(a => new ManagerAssignmentItem(
                    a.Id,
                    a.EngagementId,
                    SelectedEngagement.EngagementId,
                    SelectedEngagement.Description,
                    a.ManagerId,
                    a.Manager.Name,
                    a.Manager.Position,
                    a.BeginDate,
                    a.EndDate))
                .OrderBy(a => a.BeginDate)
                .ThenBy(a => a.ManagerName, StringComparer.OrdinalIgnoreCase)
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
        ManagerPosition ManagerPosition,
        DateTime BeginDate,
        DateTime? EndDate)
    {
        public string EngagementDisplay => string.IsNullOrWhiteSpace(EngagementId)
            ? EngagementDescription
            : LocalizationRegistry.Format(
                "Admin.ManagerAssignments.Format.EngagementDisplay",
                EngagementId,
                EngagementDescription);
    }
}
