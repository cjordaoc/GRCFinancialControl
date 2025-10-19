using CommunityToolkit.Mvvm.Messaging.Messages;

namespace InvoicePlanner.Avalonia.Messages;

public sealed class OpenModalOverlayMessage : ValueChangedMessage<object?>
{
    public OpenModalOverlayMessage(object? content, string? title = null, bool canClose = true)
        : base(content)
    {
        Title = title;
        CanClose = canClose;
    }

    public object? Content => Value;

    public string? Title { get; }

    public bool CanClose { get; }
}
