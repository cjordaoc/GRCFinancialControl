using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Services.Interfaces;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public class RevenueAllocationsViewModel : AllocationsViewModelBase
    {
        public RevenueAllocationsViewModel(IEngagementService engagementService,
                                           IFiscalYearService fiscalYearService,
                                           IDialogService dialogService,
                                           IMessenger messenger)
            : base(engagementService, fiscalYearService, dialogService, messenger)
        {
        }

        protected override AllocationKind Kind => AllocationKind.Revenue;

        public override string Header => "Revenue Allocation";
    }
}
