using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Services;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public class RevenueAllocationsViewModel : AllocationsViewModelBase
    {
        public RevenueAllocationsViewModel(IEngagementManagementFacade engagementFacade,
                                           IFiscalYearService fiscalYearService,
                                           IAllocationSnapshotService allocationSnapshotService,
                                           ISettingsService settingsService,
                                           DialogService dialogService,
                                           IMessenger messenger)
            : base(engagementFacade, fiscalYearService, allocationSnapshotService, settingsService, dialogService, messenger)
        {
        }

        public override string Header => "Revenue Allocation";
    }
}
