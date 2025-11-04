using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using App.Presentation.Localization;
using App.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;
using GRC.Shared.UI.Messages;

namespace GRCFinancialControl.Avalonia.ViewModels.Dialogs
{
    public partial class ManagerSelectionViewModel : ViewModelBase
    {
        private readonly IEngagementService _engagementService;
        private readonly IManagerService _managerService;
        private readonly Engagement _engagement;

        [ObservableProperty]
        private ObservableCollection<ManagerSelectionItem> _availableManagers = new();

        public string Title => LocalizationRegistry.Get("FINC_Admin_ManagerAssignments_Title_Editor");

        public ManagerSelectionViewModel(
            Engagement engagement,
            IEngagementService engagementService,
            IManagerService managerService,
            IMessenger messenger)
            : base(messenger)
        {
            _engagement = engagement ?? throw new ArgumentNullException(nameof(engagement));
            _engagementService = engagementService ?? throw new ArgumentNullException(nameof(engagementService));
            _managerService = managerService ?? throw new ArgumentNullException(nameof(managerService));
        }

        public override async Task LoadDataAsync()
        {
            var allManagers = await _managerService.GetAllAsync();
            var assignedManagerIds = _engagement.ManagerAssignments.Select(a => a.ManagerId).ToHashSet();
            
            var available = allManagers
                .Where(m => !assignedManagerIds.Contains(m.Id))
                .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                .Select(m => new ManagerSelectionItem(m))
                .ToList();

            AvailableManagers = new ObservableCollection<ManagerSelectionItem>(available);
        }

        [RelayCommand(CanExecute = nameof(CanSave))]
        private async Task SaveAsync()
        {
            var selectedManagers = AvailableManagers.Where(m => m.IsSelected).Select(m => m.Manager).ToList();
            if (selectedManagers.Count == 0)
            {
                return;
            }

            var fullEngagement = await _engagementService.GetByIdAsync(_engagement.Id);
            if (fullEngagement is null)
            {
                ToastService.ShowError(
                    "FINC_Admin_ManagerAssignments_Toast_OperationFailed",
                    LocalizationRegistry.Get("FINC_Admin_ManagerAssignments_Error_EngagementMissing"));
                return;
            }

            var assignedManagerIds = fullEngagement.ManagerAssignments.Select(a => a.ManagerId).ToHashSet();
            var newAssignments = selectedManagers
                .Where(m => !assignedManagerIds.Contains(m.Id))
                .ToList();

            foreach (var manager in newAssignments)
            {
                fullEngagement.ManagerAssignments.Add(new EngagementManagerAssignment
                {
                    EngagementId = fullEngagement.Id,
                    ManagerId = manager.Id
                });
            }

            try
            {
                await _engagementService.UpdateAsync(fullEngagement);
                if (newAssignments.Count > 0)
                {
                    ToastService.ShowSuccess(
                        "FINC_Admin_ManagerAssignments_Toast_SaveSuccess",
                        string.Join(", ", newAssignments.Select(m => m.Name)),
                        fullEngagement.EngagementId ?? fullEngagement.Description);
                }
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

        private bool CanSave() => AvailableManagers.Any(m => m.IsSelected);
    }

    public sealed partial class ManagerSelectionItem : ObservableObject
    {
        [ObservableProperty]
        private bool _isSelected;

        public Manager Manager { get; }

        public ManagerSelectionItem(Manager manager)
        {
            Manager = manager ?? throw new ArgumentNullException(nameof(manager));
        }
    }
}

