using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using App.Presentation.Localization;
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
    public partial class PapdSelectionViewModel : ViewModelBase
    {
        private readonly IEngagementManagementFacade _engagementFacade;
        private readonly Engagement _engagement;

        [ObservableProperty]
        private ObservableCollection<PapdSelectionItem> _availablePapds = new();

        public string Title => LocalizationRegistry.Get("FINC_Admin_PapdAssignments_Title_Editor");

        public PapdSelectionViewModel(
            Engagement engagement,
            IEngagementManagementFacade engagementFacade,
            IMessenger messenger)
            : base(messenger)
        {
            _engagement = engagement ?? throw new ArgumentNullException(nameof(engagement));
            _engagementFacade = engagementFacade ?? throw new ArgumentNullException(nameof(engagementFacade));
        }

        public override async Task LoadDataAsync()
        {
            var allPapds = await _engagementFacade.GetAllPapdsAsync();
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
                .ToList();

            try
            {
                // Update each PAPD assignment through the facade
                var currentPapdIds = _engagement.EngagementPapds.Select(p => p.PapdId).ToHashSet();
                
                // Remove unselected
                foreach (var papdId in currentPapdIds.Where(id => !selectedPapdIds.Contains(id)))
                {
                    await _engagementFacade.RemovePapdAsync(_engagement.Id, papdId);
                }
                
                // Add newly selected
                foreach (var papdId in selectedPapdIds.Where(id => !currentPapdIds.Contains(id)))
                {
                    await _engagementFacade.AssignPapdAsync(_engagement.Id, papdId);
                }

                var engagement = await _engagementFacade.GetEngagementAsync(_engagement.Id);
                var engagementDisplay = engagement?.EngagementId ?? engagement?.Description ?? _engagement.Description;

                ToastService.ShowSuccess(
                    "FINC_Admin_PapdAssignments_Toast_SaveSuccess",
                    string.Join(", ", AvailablePapds
                        .Where(p => p.IsSelected)
                        .Select(p => p.Papd.Name)),
                    engagementDisplay);

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

