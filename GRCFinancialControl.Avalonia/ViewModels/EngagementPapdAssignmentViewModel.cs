using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class EngagementPapdAssignmentViewModel : ViewModelBase
    {
        private readonly IEngagementService _engagementService;
        private readonly IPapdService _papdService;
        private readonly IMessenger _messenger;

        [ObservableProperty]
        private ObservableCollection<Engagement> _engagements = new();

        [ObservableProperty]
        private ObservableCollection<Papd> _papds = new();

        [ObservableProperty]
        private Engagement? _selectedEngagement;

        [ObservableProperty]
        private Papd? _selectedPapd;

        public EngagementPapdAssignmentViewModel(IEngagementService engagementService,
                                                 IPapdService papdService,
                                                 IMessenger messenger)
        {
            _engagementService = engagementService;
            _papdService = papdService;
            _messenger = messenger;
            AssignCommand = new RelayCommand(Assign, () => SelectedEngagement != null && SelectedPapd != null);
            UnassignCommand = new RelayCommand(Unassign, () => SelectedEngagement != null && SelectedEngagement.EngagementPapds.Count > 0);
            SaveCommand = new AsyncRelayCommand(SaveAsync);
            CloseCommand = new RelayCommand(Close);
        }

        public IRelayCommand AssignCommand { get; }
        public IRelayCommand UnassignCommand { get; }
        public IAsyncRelayCommand SaveCommand { get; }
        public IRelayCommand CloseCommand { get; }

        public override async Task LoadDataAsync()
        {
            Engagements = new ObservableCollection<Engagement>(await _engagementService.GetAllAsync());
            Papds = new ObservableCollection<Papd>(await _papdService.GetAllAsync());
        }

        private void Assign()
        {
            if (SelectedEngagement is null || SelectedPapd is null)
            {
                return;
            }

            // In a real app, we would also ask for an effective date.
            // For now, we just add the assignment.
            SelectedEngagement.EngagementPapds.Add(new EngagementPapd
            {
                PapdId = SelectedPapd.Id,
                Papd = SelectedPapd,
                EffectiveDate = System.DateTime.Today
            });
        }

        private void Unassign()
        {
            if (SelectedEngagement?.EngagementPapds is not { Count: > 0 } assignments)
            {
                return;
            }

            // In a real app, we would select a specific assignment to remove.
            // For now, we just remove the last one.
            var lastAssignment = assignments.LastOrDefault();
            if (lastAssignment is not null)
            {
                assignments.Remove(lastAssignment);
            }
        }

        private async Task SaveAsync()
        {
            if (SelectedEngagement != null)
            {
                await _engagementService.UpdateAsync(SelectedEngagement);
                _messenger.Send(new CloseDialogMessage(true));
            }
        }

        private void Close()
        {
            _messenger.Send(new CloseDialogMessage(false));
        }

        partial void OnSelectedEngagementChanged(Engagement? value)
        {
            NotifyCommandStates();
        }

        partial void OnSelectedPapdChanged(Papd? value)
        {
            NotifyCommandStates();
        }

        private void NotifyCommandStates()
        {
            if (AssignCommand is RelayCommand assign)
            {
                assign.NotifyCanExecuteChanged();
            }

            if (UnassignCommand is RelayCommand unassign)
            {
                unassign.NotifyCanExecuteChanged();
            }
        }
    }
}
