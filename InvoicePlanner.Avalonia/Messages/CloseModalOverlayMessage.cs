using CommunityToolkit.Mvvm.Messaging.Messages;

namespace InvoicePlanner.Avalonia.Messages;

public sealed class CloseModalOverlayMessage : ValueChangedMessage<bool?>
{
    public CloseModalOverlayMessage(bool? value) : base(value)
    {
    }
}
