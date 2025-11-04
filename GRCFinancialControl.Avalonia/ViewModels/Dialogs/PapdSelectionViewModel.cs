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
    public partial class PapdSelectionViewModel : ViewModelBase
    {
        private readonly IEngagementService _engagementService;
        private readonly IPapdService _papdService;
        private readonly Engagement _engagement;

        [ObservableProperty]
        private ObservableCollection<Papd> _availablePapds = new();

        [ObservableProperty]
        private Papd? _selectedPapd;

        public string Title => LocalizationRegistry.Get("FINC_Admin_PapdAssignments_Title_Editor");

        public PapdSelectionViewModel(
            Engagement engagement,
            IEngagementService engagementService,
            IPapdService papdService,
            IMessenger messenger)
            : base(messenger)
        {
            _engagement = engagement ?? throw new ArgumentNullException(nameof(engagement));
            _engagementService = engagementService ?? throw new ArgumentNullException(nameof(engagementService));
            _papdService = papdService ?? throw new ArgumentNullException(nameof(papdService));
        }

        public override async Task LoadDataAsync()
        {
            var allPapds = await _papdService.GetAllAsync();
            var assignedPapdIds = _engagement.EngagementPapds.Select(a => a.PapdId).ToHashSet();
            
            var available = allPapds
                .Where(p => !assignedPapdIds.Contains(p.Id))
                .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            AvailablePapds = new ObservableCollection<Papd>(available);
            SelectedPapd = AvailablePapds.FirstOrDefault();
        }

        [RelayCommand(CanExecute = nameof(CanSave))]
        private async Task SaveAsync()
        {
            if (SelectedPapd is null)
            {
                return;
            }

            var fullEngagement = await _engagementService.GetByIdAsync(_engagement.Id);
            if (fullEngagement is null)
            {
                ToastService.ShowError(
                    "FINC_Admin_PapdAssignments_Toast_OperationFailed",
                    LocalizationRegistry.Get("FINC_Admin_PapdAssignments_Error_EngagementMissing"));
                return;
            }

            // Check if assignment already exists
            if (fullEngagement.EngagementPapds.Any(a => a.PapdId == SelectedPapd.Id))
            {
                ToastService.ShowWarning(
                    "FINC_Admin_PapdAssignments_Toast_Exists",
                    SelectedPapd.Name,
                    fullEngagement.EngagementId ?? fullEngagement.Description);
                return;
            }

            fullEngagement.EngagementPapds.Add(new EngagementPapd
            {
                EngagementId = fullEngagement.Id,
                PapdId = SelectedPapd.Id
            });

            try
            {
                await _engagementService.UpdateAsync(fullEngagement);
                ToastService.ShowSuccess(
                    "FINC_Admin_PapdAssignments_Toast_SaveSuccess",
                    SelectedPapd.Name,
                    fullEngagement.EngagementId ?? fullEngagement.Description);
                Messenger.Send(new CloseDialogMessage(true));
            }
            catch (Exception ex)
            {
                ToastService.ShowError("FINC_Admin_PapdAssignments_Toast_OperationFailed", ex.Message);
            }
        }

        [RelayCommand]
        private void Close()
        {
            Messenger.Send(new CloseDialogMessage(false));
        }

        private bool CanSave() => SelectedPapd is not null;

        partial void OnSelectedPapdChanged(Papd? value)
        {
            SaveCommand.NotifyCanExecuteChanged();
        }
    }
}

