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
        private ObservableCollection<PapdSelectionItem> _availablePapds = new();

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
                .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .Select(p =>
                {
                    var item = new PapdSelectionItem(p)
                    {
                        IsSelected = assignedPapdIds.Contains(p.Id)
                    };
                    item.PropertyChanged += (_, _) => SaveCommand.NotifyCanExecuteChanged();
                    return item;
                })
                .ToList();

            AvailablePapds = new ObservableCollection<PapdSelectionItem>(available);
            SaveCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand(CanExecute = nameof(CanSave))]
        private async Task SaveAsync()
        {
            var selectedPapdIds = AvailablePapds
                .Where(p => p.IsSelected)
                .Select(p => p.Papd.Id)
                .ToHashSet();

            var fullEngagement = await _engagementService.GetByIdAsync(_engagement.Id);
            if (fullEngagement is null)
            {
                ToastService.ShowError(
                    "FINC_Admin_PapdAssignments_Toast_OperationFailed",
                    LocalizationRegistry.Get("FINC_Admin_PapdAssignments_Error_EngagementMissing"));
                return;
            }

            var assignedPapdIds = fullEngagement.EngagementPapds.Select(a => a.PapdId).ToHashSet();
            var newAssignments = selectedPapdIds.Except(assignedPapdIds).ToList();
            var removedAssignments = fullEngagement.EngagementPapds
                .Where(a => !selectedPapdIds.Contains(a.PapdId))
                .ToList();

            if (newAssignments.Count == 0 && removedAssignments.Count == 0)
            {
                Messenger.Send(new CloseDialogMessage(false));
                return;
            }

            foreach (var papdId in newAssignments)
            {
                fullEngagement.EngagementPapds.Add(new EngagementPapd
                {
                    EngagementId = fullEngagement.Id,
                    PapdId = papdId
                });
            }

            foreach (var assignment in removedAssignments)
            {
                fullEngagement.EngagementPapds.Remove(assignment);
            }

            try
            {
                await _engagementService.UpdateAsync(fullEngagement);
                var engagementDisplay = fullEngagement.EngagementId ?? fullEngagement.Description;

                if (newAssignments.Count > 0)
                {
                    ToastService.ShowSuccess(
                        "FINC_Admin_PapdAssignments_Toast_SaveSuccess",
                        string.Join(", ", AvailablePapds
                            .Where(p => newAssignments.Contains(p.Papd.Id))
                            .Select(p => p.Papd.Name)),
                        engagementDisplay);
                }

                if (removedAssignments.Count > 0)
                {
                    ToastService.ShowSuccess(
                        "FINC_Admin_PapdAssignments_Toast_DeleteSuccess",
                        string.Join(", ", removedAssignments
                            .Select(a => AvailablePapds.FirstOrDefault(p => p.Papd.Id == a.PapdId)?.Papd.Name)
                            .Where(name => !string.IsNullOrWhiteSpace(name))),
                        engagementDisplay);
                }
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

        private bool CanSave() => AvailablePapds.Count > 0;
    }

    public sealed partial class PapdSelectionItem : ObservableObject
    {
        [ObservableProperty]
        private bool _isSelected;

        public Papd Papd { get; }

        public PapdSelectionItem(Papd papd)
        {
            Papd = papd ?? throw new ArgumentNullException(nameof(papd));
        }
    }
}

