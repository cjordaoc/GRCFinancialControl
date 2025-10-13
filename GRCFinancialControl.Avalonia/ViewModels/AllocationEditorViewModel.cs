using System.Collections.Generic;
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
    public partial class AllocationEditorViewModel : ViewModelBase
    {
        private readonly IEngagementService _engagementService;
        private readonly IMessenger _messenger;

        [ObservableProperty]
        private Engagement _engagement;

        [ObservableProperty]
        private ObservableCollection<AllocationEntry> _allocations;

        [ObservableProperty]
        private double _totalPlannedHours;

        [ObservableProperty]
        private double _currentHoursAllocation;

        public AllocationEditorViewModel(Engagement engagement, List<FiscalYear> fiscalYears, IEngagementService engagementService, IMessenger messenger)
        {
            _engagement = engagement;
            _engagementService = engagementService;
            _messenger = messenger;
            TotalPlannedHours = engagement.TotalPlannedHours;

            Allocations = new ObservableCollection<AllocationEntry>(
                fiscalYears.Select(fy => new AllocationEntry
                {
                    FiscalYear = fy,
                    PlannedHours = engagement.Allocations.FirstOrDefault(a => a.FiscalYearId == fy.Id)?.PlannedHours ?? 0
                })
            );

            CurrentHoursAllocation = Allocations.Sum(a => a.PlannedHours);

            foreach (var allocation in Allocations)
            {
                allocation.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(AllocationEntry.PlannedHours))
                    {
                        CurrentHoursAllocation = Allocations.Sum(a => a.PlannedHours);
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
                    PlannedHours = allocation.PlannedHours
                });
            }

            await _engagementService.UpdateAsync(Engagement);
            _messenger.Send(new CloseDialogMessage(true));
        }

        [RelayCommand]
        private void Close()
        {
            _messenger.Send(new CloseDialogMessage(false));
        }
    }

    public partial class AllocationEntry : ObservableObject
    {
        [ObservableProperty]
        private FiscalYear _fiscalYear = null!;

        [ObservableProperty]
        private double _plannedHours;
    }
}