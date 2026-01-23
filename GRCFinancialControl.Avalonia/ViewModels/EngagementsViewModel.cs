using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using App.Presentation.Localization;
using App.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRC.Shared.Core.Services;
using GRC.Shared.UI.Messages;
using GRCFinancialControl.Avalonia.Services;
using GRCFinancialControl.Avalonia.ViewModels.Dialogs;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class EngagementsViewModel : ViewModelBase
    {
        private readonly IEngagementManagementFacade _engagementFacade;
        private readonly DialogService _dialogService;
        private readonly FilePickerService _filePickerService;
        private readonly ITabDelimitedExportService _exportService;
        private ObservableCollection<Engagement> _allEngagements = new();

        [ObservableProperty]
        private Engagement? _selectedEngagement;

        [ObservableProperty]
        private string _filterText = string.Empty;

        public EngagementsViewModel(
            IEngagementManagementFacade engagementFacade,
            DialogService dialogService,
            FilePickerService filePickerService,
            ITabDelimitedExportService exportService,
            IMessenger messenger)
            : base(messenger)
        {
            _engagementFacade = engagementFacade ?? throw new ArgumentNullException(nameof(engagementFacade));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _filePickerService = filePickerService ?? throw new ArgumentNullException(nameof(filePickerService));
            _exportService = exportService ?? throw new ArgumentNullException(nameof(exportService));
        }

        [ObservableProperty]
        private ObservableCollection<Engagement> _engagements = new();

        public bool HasEngagements => Engagements.Count > 0;

        public override async Task LoadDataAsync()
        {
            _allEngagements = new ObservableCollection<Engagement>(await _engagementFacade.GetAllEngagementsAsync());
            ApplyFilter();
        }

        partial void OnFilterTextChanged(string value)
        {
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            if (string.IsNullOrWhiteSpace(FilterText))
            {
                Engagements = new ObservableCollection<Engagement>(_allEngagements);
            }
            else
            {
                var filtered = _allEngagements
                    .Where(e => e.EngagementId.Contains(FilterText, StringComparison.OrdinalIgnoreCase)
                             || (e.Description?.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ?? false)
                             || (e.CustomerName?.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ?? false))
                    .ToList();
                Engagements = new ObservableCollection<Engagement>(filtered);
            }
            OnPropertyChanged(nameof(HasEngagements));
        }

        [RelayCommand]
        private async Task Add()
        {
            var editorViewModel = new EngagementEditorViewModel(
                new Engagement(), 
                _engagementFacade, 
                Messenger, 
                _dialogService);
            await _dialogService.ShowDialogAsync(editorViewModel);
            Messenger.Send(new RefreshViewMessage(RefreshTargets.FinancialData));
        }

        [RelayCommand(CanExecute = nameof(CanEdit))]
        private async Task Edit(Engagement engagement)
        {
            ArgumentNullException.ThrowIfNull(engagement);

            var fullEngagement = await _engagementFacade.GetEngagementAsync(engagement.Id);
            if (fullEngagement is null)
            {
                ToastService.ShowWarning("FINC_Engagements_Toast_NotFound", engagement.EngagementId);
                return;
            }

            var editorViewModel = new EngagementEditorViewModel(fullEngagement, _engagementFacade, Messenger, _dialogService);
            await _dialogService.ShowDialogAsync(editorViewModel);
            Messenger.Send(new RefreshViewMessage(RefreshTargets.FinancialData));
        }

        [RelayCommand(CanExecute = nameof(CanView))]
        private async Task View(Engagement engagement)
        {
            ArgumentNullException.ThrowIfNull(engagement);

            var fullEngagement = await _engagementFacade.GetEngagementAsync(engagement.Id);
            if (fullEngagement is null)
            {
                ToastService.ShowWarning("FINC_Engagements_Toast_NotFound", engagement.EngagementId);
                return;
            }

            var editorViewModel = new EngagementEditorViewModel(fullEngagement, _engagementFacade, Messenger, _dialogService, isReadOnlyMode: true);
            await _dialogService.ShowDialogAsync(editorViewModel);
        }

        [RelayCommand(CanExecute = nameof(CanDelete))]
        private async Task Delete(Engagement engagement)
        {
            if (engagement == null)
            {
                return;
            }

            try
            {
                await _engagementFacade.DeleteEngagementAsync(engagement.Id);
                ToastService.ShowSuccess("FINC_Engagements_Toast_DeleteSuccess", engagement.EngagementId);
                Messenger.Send(new RefreshViewMessage(RefreshTargets.FinancialData));
            }
            catch (InvalidOperationException ex)
            {
                ToastService.ShowWarning("FINC_Engagements_Toast_OperationFailed", ex.Message);
            }
            catch (Exception ex)
            {
                ToastService.ShowError("FINC_Engagements_Toast_OperationFailed", ex.Message);
            }
        }

        [RelayCommand(CanExecute = nameof(CanDeleteData))]
        private async Task DeleteData(Engagement engagement)
        {
            if (engagement is null)
            {
                return;
            }

            var result = await _dialogService.ShowConfirmationAsync(
                LocalizationRegistry.Get("FINC_Dialog_DeleteData_Title"),
                LocalizationRegistry.Format("FINC_Dialog_DeleteData_Message", engagement.EngagementId));
            if (result)
            {
                try
                {
                    await _engagementFacade.DeleteEngagementDataAsync(engagement.Id);
                    ToastService.ShowSuccess("FINC_Engagements_Toast_ReverseSuccess", engagement.EngagementId);
                    Messenger.Send(new RefreshViewMessage(RefreshTargets.FinancialData));
                }
                catch (InvalidOperationException ex)
                {
                    ToastService.ShowWarning("FINC_Engagements_Toast_OperationFailed", ex.Message);
                }
                catch (Exception ex)
                {
                    ToastService.ShowError("FINC_Engagements_Toast_OperationFailed", ex.Message);
                }
            }
        }

        [RelayCommand(CanExecute = nameof(CanAssign))]
        private async Task AssignPapd()
        {
            if (SelectedEngagement is null)
            {
                return;
            }

            var fullEngagement = await _engagementFacade.GetEngagementAsync(SelectedEngagement.Id);
            if (fullEngagement is null)
            {
                return;
            }

            var availablePapds = await _engagementFacade.GetAvailablePapdsAsync(fullEngagement);
            
            if (availablePapds.Count == 0)
            {
                ToastService.ShowWarning("FINC_Engagements_Toast_AllPapdsAssigned");
                return;
            }

            // Use the selection view model for choosing a PAPD
            var selectionViewModel = new PapdSelectionViewModel(
                fullEngagement,
                _engagementFacade,
                Messenger);

            await selectionViewModel.LoadDataAsync();
            await _dialogService.ShowDialogAsync(selectionViewModel, selectionViewModel.Title);
            Messenger.Send(new RefreshViewMessage(RefreshTargets.FinancialData));
        }

        [RelayCommand(CanExecute = nameof(CanAssign))]
        private async Task AssignManager()
        {
            if (SelectedEngagement is null)
            {
                return;
            }

            var fullEngagement = await _engagementFacade.GetEngagementAsync(SelectedEngagement.Id);
            if (fullEngagement is null)
            {
                return;
            }

            var availableManagers = await _engagementFacade.GetAvailableManagersAsync(fullEngagement);
            
            if (availableManagers.Count == 0)
            {
                ToastService.ShowWarning("FINC_Engagements_Toast_AllManagersAssigned");
                return;
            }

            var selectionViewModel = new ManagerSelectionViewModel(
                fullEngagement,
                _engagementFacade,
                Messenger);

            await selectionViewModel.LoadDataAsync();
            await _dialogService.ShowDialogAsync(selectionViewModel, selectionViewModel.Title);
            Messenger.Send(new RefreshViewMessage(RefreshTargets.FinancialData));
        }

        [RelayCommand]
        private async Task Export()
        {
            if (Engagements.Count == 0)
            {
                ToastService.ShowWarning("FINC_Toast_NoDataToExport");
                return;
            }

            try
            {
                var fileName = $"Engagements_{DateTime.Now:yyyyMMdd_HHmmss}";
                var filePath = await _filePickerService.SaveFileAsync(
                    fileName,
                    "Export Engagements",
                    ".txt");
                
                if (string.IsNullOrEmpty(filePath))
                {
                    return;
                }

                var headers = new[]
                {
                    "ID", "Engagement ID", "Description", "Customer Name", 
                    "Opening Value", "Status", "Estimated Hours", "Planned Hours"
                };

                var rows = Engagements.Select(engagement => new[]
                {
                    engagement.Id.ToString(),
                    engagement.EngagementId ?? "",
                    engagement.Description ?? "",
                    engagement.CustomerName ?? "",
                    engagement.OpeningValue.ToString("C"),
                    engagement.StatusText ?? "",
                    engagement.EstimatedToCompleteHours.ToString("N2"),
                    engagement.InitialHoursBudget.ToString("N2")
                }.AsEnumerable());

                await _exportService.ExportAsync(filePath, headers, rows);
                ToastService.ShowSuccess("FINC_Toast_ExportSuccess", Path.GetFileName(filePath));
            }
            catch (Exception ex)
            {
                ToastService.ShowError("FINC_Toast_ExportFailed", ex.Message);
            }
        }

        private static bool CanEdit(Engagement engagement) => engagement is not null;

        private static bool CanView(Engagement engagement) => engagement is not null;

        private static bool CanDelete(Engagement engagement) => engagement is not null;

        private static bool CanDeleteData(Engagement engagement) => engagement is not null;

        private bool CanAssign() => SelectedEngagement is not null;

        partial void OnSelectedEngagementChanged(Engagement? value)
        {
            EditCommand.NotifyCanExecuteChanged();
            ViewCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
            DeleteDataCommand.NotifyCanExecuteChanged();
            AssignPapdCommand.NotifyCanExecuteChanged();
            AssignManagerCommand.NotifyCanExecuteChanged();
        }

    }
}
