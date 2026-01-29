using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using App.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRC.Shared.UI.Messages;
using GRCFinancialControl.Avalonia.Services;
using GRCFinancialControl.Avalonia.ViewModels.Dialogs;
using GRC.Shared.Core.Models.Core;
using GRC.Shared.Core.Models.Financial;
using GRC.Shared.Core.Models.Allocations;
using GRC.Shared.Core.Models.Assignments;
using GRC.Shared.Core.Models.Lookups;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class ManagersViewModel : ViewModelBase
    {
        private readonly IManagerService _managerService;
        private readonly DialogService _dialogService;
        private ObservableCollection<Manager> _allManagers = new();

        [ObservableProperty]
        private ObservableCollection<Manager> _managers = new();

        [ObservableProperty]
        private Manager? _selectedManager;

        [ObservableProperty]
        private string _filterText = string.Empty;

        public bool HasManagers => Managers.Count > 0;

        private readonly IEngagementService _engagementService;
        private readonly IPapdService _papdService;
        private readonly ICustomerService _customerService;
        private readonly IClosingPeriodService _closingPeriodService;
        private readonly IManagerAssignmentService _managerAssignmentService;

        public ManagersViewModel(
            IManagerService managerService,
            IEngagementService engagementService,
            IPapdService papdService,
            ICustomerService customerService,
            IClosingPeriodService closingPeriodService,
            IManagerAssignmentService managerAssignmentService,
            DialogService dialogService,
            IMessenger messenger)
            : base(messenger)
        {
            _managerService = managerService;
            _engagementService = engagementService;
            _papdService = papdService;
            _customerService = customerService;
            _closingPeriodService = closingPeriodService;
            _managerAssignmentService = managerAssignmentService;
            _dialogService = dialogService;
        }

        public override async Task LoadDataAsync()
        {
            try
            {
                _allManagers = new ObservableCollection<Manager>(await _managerService.GetAllAsync());
                ApplyFilter();
            }
            catch (Exception ex)
            {
                _allManagers = new ObservableCollection<Manager>();
                ApplyFilter();
                var message = LocalizationRegistry.Format("FINC_Managers_Toast_LoadError", ex.Message);
                ToastService.ShowError(message);
                throw;
            }
        }

        partial void OnFilterTextChanged(string value)
        {
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            if (string.IsNullOrWhiteSpace(FilterText))
            {
                Managers = new ObservableCollection<Manager>(_allManagers);
            }
            else
            {
                var filtered = _allManagers
                    .Where(m => m.Name.Contains(FilterText, StringComparison.OrdinalIgnoreCase)
                             || (m.Email?.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ?? false)
                             || (m.WindowsLogin?.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ?? false))
                    .ToList();
                Managers = new ObservableCollection<Manager>(filtered);
            }
            OnPropertyChanged(nameof(HasManagers));
        }

        [RelayCommand]
        private async Task Add()
        {
            var editorViewModel = new ManagerEditorViewModel(new Manager(), _managerService, Messenger);
            await _dialogService.ShowDialogAsync(editorViewModel);
            Messenger.Send(new RefreshViewMessage(RefreshTargets.FinancialData));
        }

        [RelayCommand(CanExecute = nameof(CanModifySelection))]
        private async Task Edit(Manager manager)
        {
            if (manager is null)
            {
                return;
            }

            var editorViewModel = new ManagerEditorViewModel(manager, _managerService, Messenger);
            await _dialogService.ShowDialogAsync(editorViewModel);
            Messenger.Send(new RefreshViewMessage(RefreshTargets.FinancialData));
        }

        [RelayCommand(CanExecute = nameof(CanModifySelection))]
        private async Task View(Manager manager)
        {
            if (manager is null)
            {
                return;
            }

            var editorViewModel = new ManagerEditorViewModel(manager, _managerService, Messenger, isReadOnlyMode: true);
            await _dialogService.ShowDialogAsync(editorViewModel);
        }

        [RelayCommand(CanExecute = nameof(CanModifySelection))]
        private async Task Delete(Manager manager)
        {
            if (manager is null)
            {
                return;
            }

            try
            {
                await _managerService.DeleteAsync(manager.Id);
                var message = LocalizationRegistry.Format("FINC_Managers_Toast_DeleteSuccess", manager.Name);
                ToastService.ShowSuccess(message);
                Messenger.Send(new RefreshViewMessage(RefreshTargets.FinancialData));
            }
            catch (System.Exception ex)
            {
                var message = LocalizationRegistry.Format("FINC_Managers_Toast_OperationFailed", ex.Message);
                ToastService.ShowError(message);
            }
        }

        [RelayCommand(CanExecute = nameof(CanEditAssignment))]
        private async Task EditAssignment()
        {
            if (SelectedManager is null)
            {
                return;
            }

            var editViewModel = new EditAssignmentViewModel(SelectedManager, _managerAssignmentService, Messenger);
            await editViewModel.LoadDataAsync();
            await _dialogService.ShowDialogAsync(editViewModel, editViewModel.Title);
        }

        private bool CanModifySelection(Manager? manager) => manager is not null;
        private bool CanEditAssignment() => SelectedManager is not null;

        partial void OnSelectedManagerChanged(Manager? value)
        {
            EditCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
            ViewCommand.NotifyCanExecuteChanged();
            EditAssignmentCommand.NotifyCanExecuteChanged();
        }
    }
}
