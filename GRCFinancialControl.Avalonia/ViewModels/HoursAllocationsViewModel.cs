using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Services.Interfaces;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public class HoursAllocationsViewModel : AllocationsViewModelBase
    {
        public HoursAllocationsViewModel(IEngagementService engagementService,
                                         IFiscalYearService fiscalYearService,
                                         IDialogService dialogService,
                                         IMessenger messenger)
            : base(engagementService, fiscalYearService, dialogService, messenger)
        {
        }

        protected override AllocationKind Kind => AllocationKind.Hours;

        public override string Header => "Hours Allocation";
    }
}
