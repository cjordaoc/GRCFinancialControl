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
        private readonly IManagerAssignmentService _managerAssignmentService;
        private readonly Engagement _engagement;

        [ObservableProperty]
        private ObservableCollection<ManagerSelectionItem> _availableManagers = new();

        public string Title => LocalizationRegistry.Get("FINC_Admin_ManagerAssignments_Title_Editor");

        public ManagerSelectionViewModel(
            Engagement engagement,
            IEngagementService engagementService,
            IManagerService managerService,
            IManagerAssignmentService managerAssignmentService,
            IMessenger messenger)
            : base(messenger)
        {
            _engagement = engagement ?? throw new ArgumentNullException(nameof(engagement));
            _engagementService = engagementService ?? throw new ArgumentNullException(nameof(engagementService));
            _managerService = managerService ?? throw new ArgumentNullException(nameof(managerService));
            _managerAssignmentService = managerAssignmentService ?? throw new ArgumentNullException(nameof(managerAssignmentService));
        }

        public override async Task LoadDataAsync()
        {
            var allManagers = await _managerService.GetAllAsync();
            var assignedManagerIds = _engagement.ManagerAssignments.Select(a => a.ManagerId).ToHashSet();

            var available = allManagers
                .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                .Select(m =>
                {
                    var item = new ManagerSelectionItem(m)
                    {
                        IsSelected = assignedManagerIds.Contains(m.Id)
                    };
                    item.PropertyChanged += (_, _) => SaveCommand.NotifyCanExecuteChanged();
                    return item;
                })
                .ToList();

            AvailableManagers = new ObservableCollection<ManagerSelectionItem>(available);
            SaveCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand(CanExecute = nameof(CanSave))]
        private async Task SaveAsync()
        {
            var selectedManagerIds = AvailableManagers
                .Where(m => m.IsSelected)
                .Select(m => m.Manager.Id)
                .ToList();

            try
            {
                // Use the dedicated service for updating manager assignments
                await _managerAssignmentService.UpdateAssignmentsForEngagementAsync(_engagement.Id, selectedManagerIds);

                var engagement = await _engagementService.GetByIdAsync(_engagement.Id);
                var engagementDisplay = engagement?.EngagementId ?? engagement?.Description ?? _engagement.Description;

                ToastService.ShowSuccess(
                    "FINC_Admin_ManagerAssignments_Toast_SaveSuccess",
                    string.Join(", ", AvailableManagers
                        .Where(m => m.IsSelected)
                        .Select(m => m.Manager.Name)),
                    engagementDisplay);

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

        private bool CanSave() => AvailableManagers.Count > 0;
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

