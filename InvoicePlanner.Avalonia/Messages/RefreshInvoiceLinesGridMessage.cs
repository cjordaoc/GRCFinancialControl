using CommunityToolkit.Mvvm.Messaging.Messages;

namespace InvoicePlanner.Avalonia.Messages;

public sealed class RefreshInvoiceLinesGridMessage : ValueChangedMessage<bool>
{
    public RefreshInvoiceLinesGridMessage()
        : base(true)
    {
    }
}
