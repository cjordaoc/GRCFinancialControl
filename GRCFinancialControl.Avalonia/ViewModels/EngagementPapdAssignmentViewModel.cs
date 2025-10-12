using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class EngagementPapdAssignmentViewModel : ViewModelBase
    {
        private readonly IEngagementService _engagementService;
        private readonly IPapdService _papdService;

        [ObservableProperty]
        private ObservableCollection<Engagement> _engagements = new();

        [ObservableProperty]
        private ObservableCollection<Papd> _papds = new();

        [ObservableProperty]
        private Engagement? _selectedEngagement;

        [ObservableProperty]
        private Papd? _selectedPapd;

        public EngagementPapdAssignmentViewModel(IEngagementService engagementService, IPapdService papdService)
        {
            _engagementService = engagementService;
            _papdService = papdService;
            LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
            AssignCommand = new RelayCommand(Assign, () => SelectedEngagement != null && SelectedPapd != null);
            UnassignCommand = new RelayCommand(Unassign, () => SelectedEngagement != null && SelectedEngagement.EngagementPapds.Count > 0);
            SaveCommand = new AsyncRelayCommand(SaveAsync);
            _ = LoadDataCommand.ExecuteAsync(null);
        }

        public IAsyncRelayCommand LoadDataCommand { get; }
        public IRelayCommand AssignCommand { get; }
        public IRelayCommand UnassignCommand { get; }
        public IAsyncRelayCommand SaveCommand { get; }

        private async Task LoadDataAsync()
        {
            Engagements = new ObservableCollection<Engagement>(await _engagementService.GetAllAsync());
            Papds = new ObservableCollection<Papd>(await _papdService.GetAllAsync());
        }

        private void Assign()
        {
            // In a real app, we would also ask for an effective date.
            // For now, we just add the assignment.
            SelectedEngagement.EngagementPapds.Add(new EngagementPapd { Papd = SelectedPapd, EffectiveDate = System.DateTime.Today });
        }

        private void Unassign()
        {
            // In a real app, we would select a specific assignment to remove.
            // For now, we just remove the last one.
            if (SelectedEngagement.EngagementPapds.Count > 0)
            {
                SelectedEngagement.EngagementPapds.RemoveAt(SelectedEngagement.EngagementPapds.Count - 1);
            }
        }

        private async Task SaveAsync()
        {
            if (SelectedEngagement != null)
            {
                await _engagementService.UpdateAsync(SelectedEngagement);
            }
        }
    }
}