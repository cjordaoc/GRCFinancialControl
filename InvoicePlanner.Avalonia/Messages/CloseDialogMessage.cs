using CommunityToolkit.Mvvm.Messaging.Messages;

namespace InvoicePlanner.Avalonia.Messages
{
    public sealed class CloseDialogMessage : ValueChangedMessage<bool>
    {
        public CloseDialogMessage(bool value) : base(value)
        {
        }
    }
}
