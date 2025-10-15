using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class AllocationsViewModel : ViewModelBase
    {
        public HoursAllocationsViewModel Hours { get; }

        public RevenueAllocationsViewModel Revenue { get; }

        [ObservableProperty]
        private ViewModelBase? _selectedAllocation;

        public AllocationsViewModel(HoursAllocationsViewModel hoursAllocations,
                                    RevenueAllocationsViewModel revenueAllocations,
                                    IMessenger messenger)
            : base(messenger)
        {
            Hours = hoursAllocations;
            Revenue = revenueAllocations;
            SelectedAllocation = Hours;
        }

        public override async Task LoadDataAsync()
        {
            await Hours.LoadDataAsync();
            await Revenue.LoadDataAsync();

            SelectedAllocation ??= Hours;
        }
    }
}
