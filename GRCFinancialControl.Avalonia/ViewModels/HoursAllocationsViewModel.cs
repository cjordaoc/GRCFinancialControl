using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;
using GRCFinancialControl.Avalonia.Services.Interfaces;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class HoursAllocationsViewModel : AllocationsViewModelBase
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

        [RelayCommand]
        private async Task RefreshAssignmentsAsync()
        {
            await base.LoadDataAsync();
            Messenger.Send(new ForecastOperationRequestMessage(ForecastOperationRequestType.Refresh));
        }

        [RelayCommand]
        private void GenerateTemplateRetain()
        {
            Messenger.Send(new ForecastOperationRequestMessage(ForecastOperationRequestType.GenerateTemplateRetain));
        }

        [RelayCommand]
        private void ExportPending()
        {
            Messenger.Send(new ForecastOperationRequestMessage(ForecastOperationRequestType.ExportPending));
        }
    }
}
