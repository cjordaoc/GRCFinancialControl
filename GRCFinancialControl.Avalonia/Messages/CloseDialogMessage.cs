using CommunityToolkit.Mvvm.Messaging.Messages;

namespace GRCFinancialControl.Avalonia.Messages
{
    public class CloseDialogMessage : ValueChangedMessage<bool>
    {
        public CloseDialogMessage(bool value) : base(value)
        {
        }
    }
}