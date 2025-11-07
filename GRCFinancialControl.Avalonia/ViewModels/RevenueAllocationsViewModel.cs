using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Services;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public class RevenueAllocationsViewModel : AllocationsViewModelBase
    {
        public RevenueAllocationsViewModel(IEngagementService engagementService,
                                           IFiscalYearService fiscalYearService,
                                           ICustomerService customerService,
                                           IClosingPeriodService closingPeriodService,
                                           IAllocationSnapshotService allocationSnapshotService,
                                           DialogService dialogService,
                                           IMessenger messenger)
            : base(engagementService, fiscalYearService, customerService, closingPeriodService, allocationSnapshotService, dialogService, messenger)
        {
        }

        public override string Header => "Revenue Allocation";
    }
}
