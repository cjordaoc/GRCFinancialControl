using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class AllocationViewModel : ViewModelBase
    {
        private readonly IEngagementService _engagementService;
        private readonly IFiscalYearService _fiscalYearService;
        private readonly IPlannedAllocationService _allocationService;

        [ObservableProperty]
        private ObservableCollection<Engagement> _engagements = new();

        [ObservableProperty]
        private Engagement? _selectedEngagement;

        [ObservableProperty]
        private ObservableCollection<FiscalYear> _fiscalYears = new();

        [ObservableProperty]
        private ObservableCollection<AllocationEntryViewModel> _allocationEntries = new();

        [ObservableProperty]
        private double _totalAllocatedHours;

        [ObservableProperty]
        private double _remainingHours;

        public AllocationViewModel(IEngagementService engagementService, IFiscalYearService fiscalYearService, IPlannedAllocationService allocationService)
        {
            _engagementService = engagementService;
            _fiscalYearService = fiscalYearService;
            _allocationService = allocationService;
            OnEngagementSelectedCommand = new AsyncRelayCommand(OnEngagementSelectedAsync);
            RecalculateTotalsCommand = new RelayCommand(RecalculateTotals);
            SaveAllocationsCommand = new AsyncRelayCommand(SaveAllocationsAsync);
        }

        public IAsyncRelayCommand OnEngagementSelectedCommand { get; }
        public IRelayCommand RecalculateTotalsCommand { get; }
        public IAsyncRelayCommand SaveAllocationsCommand { get; }

        public override async Task LoadDataAsync()
        {
            Engagements = new ObservableCollection<Engagement>(await _engagementService.GetAllAsync());
            FiscalYears = new ObservableCollection<FiscalYear>(await _fiscalYearService.GetAllAsync());
        }

        private async Task OnEngagementSelectedAsync()
        {
            if (SelectedEngagement == null) return;

            AllocationEntries.Clear();
            var existingAllocations = await _allocationService.GetAllocationsForEngagementAsync(SelectedEngagement.Id);

            foreach (var fy in FiscalYears)
            {
                var existing = existingAllocations.FirstOrDefault(a => a.ClosingPeriodId == fy.Id);
                AllocationEntries.Add(new AllocationEntryViewModel(fy, existing?.AllocatedHours ?? 0));
            }
            RecalculateTotals();
        }

        private void RecalculateTotals()
        {
            if (SelectedEngagement == null) return;

            TotalAllocatedHours = AllocationEntries.Sum(e => e.AllocatedHours);
            RemainingHours = SelectedEngagement.TotalPlannedHours - TotalAllocatedHours;
        }

        private async Task SaveAllocationsAsync()
        {
            if (SelectedEngagement == null) return;

            var allocations = AllocationEntries.Select(e => new PlannedAllocation
            {
                ClosingPeriodId = e.FiscalYear.Id,
                AllocatedHours = e.AllocatedHours
            }).ToList();

            await _allocationService.SaveAllocationsForEngagementAsync(SelectedEngagement.Id, allocations);
        }
    }

    public partial class AllocationEntryViewModel : ViewModelBase
    {
        public FiscalYear FiscalYear { get; }

        [ObservableProperty]
        private double _allocatedHours;

        public AllocationEntryViewModel(FiscalYear fiscalYear, double allocatedHours)
        {
            FiscalYear = fiscalYear;
            _allocatedHours = allocatedHours;
        }
    }
}