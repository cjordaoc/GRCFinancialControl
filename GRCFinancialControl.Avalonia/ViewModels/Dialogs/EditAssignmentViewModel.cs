using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using App.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Services;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;
using GRC.Shared.UI.Messages;

namespace GRCFinancialControl.Avalonia.ViewModels.Dialogs
{
    public partial class EditAssignmentViewModel : ViewModelBase
    {
        private readonly IManagerAssignmentService? _managerAssignmentService;
        private readonly IPapdAssignmentService? _papdAssignmentService;
        private readonly int? _managerId;
        private readonly int? _papdId;
        private readonly string _entityName;

        [ObservableProperty]
        private ObservableCollection<AssignmentItem> _assignments = new();

        [ObservableProperty]
        private AssignmentItem? _selectedAssignment;

        public string Title { get; }

        public EditAssignmentViewModel(
            Manager manager,
            IManagerAssignmentService managerAssignmentService,
            IMessenger messenger)
            : base(messenger)
        {
            _managerAssignmentService = managerAssignmentService ?? throw new ArgumentNullException(nameof(managerAssignmentService));
            _managerId = manager?.Id ?? throw new ArgumentNullException(nameof(manager));
            _entityName = manager?.Name ?? "Manager";
            Title = $"Edit Assignments - {_entityName}";
        }

        public EditAssignmentViewModel(
            Papd papd,
            IPapdAssignmentService papdAssignmentService,
            IMessenger messenger)
            : base(messenger)
        {
            _papdAssignmentService = papdAssignmentService ?? throw new ArgumentNullException(nameof(papdAssignmentService));
            _papdId = papd?.Id ?? throw new ArgumentNullException(nameof(papd));
            _entityName = papd?.Name ?? "PAPD";
            Title = $"Edit Assignments - {_entityName}";
        }

        public override async Task LoadDataAsync()
        {
            if (_managerId.HasValue && _managerAssignmentService != null)
            {
                var managerAssignments = await _managerAssignmentService.GetByManagerIdAsync(_managerId.Value);
                Assignments = new ObservableCollection<AssignmentItem>(
                    managerAssignments.Select(a => new AssignmentItem(
                        a.Id,
                        a.Engagement.EngagementId,
                        a.Engagement.Description,
                        a.Engagement.Id,
                        a.Engagement.CustomerName)));
            }
            else if (_papdId.HasValue && _papdAssignmentService != null)
            {
                var papdAssignments = await _papdAssignmentService.GetByPapdIdAsync(_papdId.Value);
                Assignments = new ObservableCollection<AssignmentItem>(
                    papdAssignments.Select(a => new AssignmentItem(
                        a.Id,
                        a.Engagement.EngagementId,
                        a.Engagement.Description,
                        a.Engagement.Id,
                        a.Engagement.CustomerName)));
            }
        }

        [RelayCommand(CanExecute = nameof(CanDeleteAssignment))]
        private async Task DeleteAssignment()
        {
            if (SelectedAssignment is null)
            {
                return;
            }

            try
            {
                if (_managerAssignmentService != null)
                {
                    await _managerAssignmentService.DeleteAsync(SelectedAssignment.AssignmentId);
                }
                else if (_papdAssignmentService != null)
                {
                    await _papdAssignmentService.DeleteAsync(SelectedAssignment.AssignmentId);
                }

                ToastService.ShowSuccess("Assignment deleted successfully.");
                Assignments.Remove(SelectedAssignment);
                SelectedAssignment = null;
                Messenger.Send(new RefreshViewMessage(RefreshTargets.FinancialData));
            }
            catch (Exception ex)
            {
                ToastService.ShowError("Failed to delete assignment.", ex.Message);
            }
        }

        [RelayCommand]
        private void Close()
        {
            Messenger.Send(new CloseDialogMessage(false));
        }

        private bool CanDeleteAssignment() => SelectedAssignment is not null;

        partial void OnSelectedAssignmentChanged(AssignmentItem? value)
        {
            DeleteAssignmentCommand.NotifyCanExecuteChanged();
        }
    }

    public record AssignmentItem(int AssignmentId, string EngagementId, string Description, int EngagementIdInternal, string CustomerName)
    {
        public string DisplayText => string.IsNullOrWhiteSpace(EngagementId)
            ? Description
            : $"{EngagementId} - {Description}";
    }
}

