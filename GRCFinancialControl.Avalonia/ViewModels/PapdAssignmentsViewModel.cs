using System;
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
    public partial class PapdAssignmentsViewModel : ViewModelBase
    {
        private readonly IPapdAssignmentService _assignmentService;
        private readonly IEngagementService _engagementService;
        private readonly IPapdService _papdService;

        [ObservableProperty]
        private ObservableCollection<EngagementOption> _engagements = new();

        [ObservableProperty]
        private EngagementOption? _selectedEngagement;

        [ObservableProperty]
        private ObservableCollection<SelectablePapd> _availablePapds = new();

        public PapdAssignmentsViewModel(
            IPapdAssignmentService assignmentService,
            IEngagementService engagementService,
            IPapdService papdService,
            IMessenger messenger)
            : base(messenger)
        {
            _assignmentService = assignmentService;
            _engagementService = engagementService;
            _papdService = papdService;
        }

        public override async Task LoadDataAsync()
        {
            var engagements = await _engagementService.GetAllAsync();
            var engagementOptions = engagements
                .OrderBy(e => e.Description, StringComparer.OrdinalIgnoreCase)
                .Select(e => new EngagementOption(e.Id, e.EngagementId, e.Description))
                .ToList();

            Engagements = new ObservableCollection<EngagementOption>(engagementOptions);

            var papds = await _papdService.GetAllAsync();
            var orderedPapds = papds
                .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .Select(p => new SelectablePapd(p))
                .ToList();

            AvailablePapds = new ObservableCollection<SelectablePapd>(orderedPapds);

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

            var selectedPapdIds = AvailablePapds
                .Where(p => p.IsSelected)
                .Select(p => p.Papd.Id)
                .ToList();

            try
            {
                await _assignmentService.UpdateAssignmentsForEngagementAsync(SelectedEngagement.InternalId, selectedPapdIds);
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
            foreach (var papd in AvailablePapds)
            {
                papd.IsSelected = false;
            }

            if (SelectedEngagement is null)
            {
                return;
            }

            var assignments = await _assignmentService.GetByEngagementIdAsync(SelectedEngagement.InternalId);
            var assignedPapdIds = assignments.Select(a => a.PapdId).ToHashSet();

            foreach (var papd in AvailablePapds)
            {
                if (assignedPapdIds.Contains(papd.Papd.Id))
                {
                    papd.IsSelected = true;
                }
            }
        }
    }

    public partial class SelectablePapd : ObservableObject
    {
        [ObservableProperty]
        private bool _isSelected;

        public Papd Papd { get; }

        public SelectablePapd(Papd papd)
        {
            Papd = papd;
        }
    }
}
