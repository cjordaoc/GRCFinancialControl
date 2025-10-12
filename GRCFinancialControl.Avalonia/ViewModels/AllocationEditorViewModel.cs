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
    public partial class AllocationEditorViewModel : ViewModelBase
    {
        private readonly IEngagementService _engagementService;

        [ObservableProperty]
        private Engagement _engagement;

        [ObservableProperty]
        private ObservableCollection<AllocationEntry> _allocations;

        [ObservableProperty]
        private double _totalPlannedHours;

        [ObservableProperty]
        private double _currentHoursAllocation;

        public AllocationEditorViewModel(Engagement engagement, List<FiscalYear> fiscalYears, IEngagementService engagementService)
        {
            _engagement = engagement;
            _engagementService = engagementService;
            TotalPlannedHours = engagement.TotalPlannedHours;

            Allocations = new ObservableCollection<AllocationEntry>(
                fiscalYears.Select(fy => new AllocationEntry
                {
                    FiscalYear = fy,
                    Hours = engagement.Allocations.FirstOrDefault(a => a.FiscalYearId == fy.Id)?.Hours ?? 0
                })
            );

            CurrentHoursAllocation = Allocations.Sum(a => a.Hours);

            foreach (var allocation in Allocations)
            {
                allocation.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(AllocationEntry.Hours))
                    {
                        CurrentHoursAllocation = Allocations.Sum(a => a.Hours);
                    }
                };
            }
        }

        [RelayCommand]
        private async Task Save()
        {
            if (CurrentHoursAllocation != TotalPlannedHours)
            {
                // In a real app, we would show a message to the user
                return;
            }

            Engagement.Allocations.Clear();
            foreach (var allocation in Allocations)
            {
                Engagement.Allocations.Add(new EngagementFiscalYearAllocation
                {
                    EngagementId = Engagement.Id,
                    FiscalYearId = allocation.FiscalYear.Id,
                    Hours = allocation.Hours
                });
            }

            await _engagementService.UpdateAsync(Engagement);
        }
    }

    public partial class AllocationEntry : ObservableObject
    {
        [ObservableProperty]
        private FiscalYear _fiscalYear = null!;

        [ObservableProperty]
        private double _hours;
    }
}