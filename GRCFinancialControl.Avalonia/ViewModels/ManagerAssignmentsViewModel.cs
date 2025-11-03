using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using App.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRC.Shared.UI.Messages;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class ManagerAssignmentsViewModel : ViewModelBase
    {
        private readonly IManagerAssignmentService _assignmentService;
        private readonly IEngagementService _engagementService;
        private readonly IManagerService _managerService;

        [ObservableProperty]
        private ObservableCollection<EngagementOption> _engagements = new();

        [ObservableProperty]
        private EngagementOption? _selectedEngagement;

        [ObservableProperty]
        private ObservableCollection<SelectableManager> _availableManagers = new();

        public ManagerAssignmentsViewModel(
            IManagerAssignmentService assignmentService,
            IEngagementService engagementService,
            IManagerService managerService,
            IMessenger messenger)
            : base(messenger)
        {
            _assignmentService = assignmentService;
            _engagementService = engagementService;
            _managerService = managerService;
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
                .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                .Select(m => new SelectableManager(m))
                .ToList();

            AvailableManagers = new ObservableCollection<SelectableManager>(orderedManagers);

            if (SelectedEngagement is null && Engagements.Any())
            {
                SelectedEngagement = Engagements.First();
            }
            else
            {
                await LoadAssignmentsForSelectedEngagementAsync();
            }

            SaveCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand(CanExecute = nameof(CanSave))]
        private async Task SaveAsync()
        {
            if (SelectedEngagement is null)
            {
                return;
            }

            var selectedManagerIds = AvailableManagers
                .Where(m => m.IsSelected)
                .Select(m => m.Manager.Id)
                .ToList();

            try
            {
                await _assignmentService.UpdateAssignmentsForEngagementAsync(SelectedEngagement.InternalId, selectedManagerIds);
                ToastService.ShowSuccess("Assignments updated successfully.");
                Messenger.Send(new RefreshViewMessage(RefreshTargets.FinancialData));
            }
            catch (Exception ex)
            {
                ToastService.ShowError("Failed to update assignments.", ex.Message);
            }
        }

        private bool CanSave() => SelectedEngagement is not null;

        partial void OnSelectedEngagementChanged(EngagementOption? value)
        {
            SaveCommand.NotifyCanExecuteChanged();
            _ = LoadAssignmentsForSelectedEngagementAsync();
        }

        private async Task LoadAssignmentsForSelectedEngagementAsync()
        {
            foreach (var manager in AvailableManagers)
            {
                manager.IsSelected = false;
            }

            if (SelectedEngagement is null)
            {
                return;
            }

            var assignments = await _assignmentService.GetByEngagementIdAsync(SelectedEngagement.InternalId);
            var assignedManagerIds = assignments.Select(a => a.ManagerId).ToHashSet();

            foreach (var manager in AvailableManagers)
            {
                if (assignedManagerIds.Contains(manager.Manager.Id))
                {
                    manager.IsSelected = true;
                }
            }
        }
    }

    public partial class SelectableManager : ObservableObject
    {
        [ObservableProperty]
        private bool _isSelected;

        public Manager Manager { get; }

        public SelectableManager(Manager manager)
        {
            Manager = manager;
        }
    }

    public record EngagementOption(int InternalId, string EngagementId, string Description)
    {
        public override string ToString() => $"__{EngagementId}__ {Description}";
    }
}
