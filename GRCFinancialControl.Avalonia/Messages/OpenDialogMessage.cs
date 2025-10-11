using CommunityToolkit.Mvvm.Messaging.Messages;
using GRCFinancialControl.Avalonia.ViewModels;

namespace GRCFinancialControl.Avalonia.Messages
{
    public class OpenDialogMessage : ValueChangedMessage<ViewModelBase>
    {
        public OpenDialogMessage(ViewModelBase viewModel) : base(viewModel)
        {
        }
    }
}