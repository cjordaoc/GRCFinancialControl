using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class ControlMasterDataViewModel : ViewModelBase
    {
        public ControlMasterDataViewModel(FiscalYearsViewModel fiscalYears,
                                          ClosingPeriodsViewModel closingPeriods,
                                          CustomersViewModel customers,
                                          IMessenger messenger)
            : base(messenger)
        {
            FiscalYears = fiscalYears;
            ClosingPeriods = closingPeriods;
            Customers = customers;

            SelectedSection = FiscalYears;
        }

        public FiscalYearsViewModel FiscalYears { get; }

        public ClosingPeriodsViewModel ClosingPeriods { get; }

        public CustomersViewModel Customers { get; }

        [ObservableProperty]
        private ViewModelBase? _selectedSection;

        public override async Task LoadDataAsync()
        {
            await FiscalYears.LoadDataAsync();
            await ClosingPeriods.LoadDataAsync();
            await Customers.LoadDataAsync();

            SelectedSection ??= FiscalYears;
        }
    }
}
