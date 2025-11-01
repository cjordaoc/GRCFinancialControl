using CommunityToolkit.Mvvm.Messaging.Messages;

namespace GRC.Shared.UI.Messages;

/// <summary>
/// Broadcasts that the active dialog should close.
/// </summary>
public sealed class CloseDialogMessage : ValueChangedMessage<bool>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CloseDialogMessage"/> class.
    /// </summary>
    /// <param name="wasConfirmed">Indicates whether the dialog confirmed the requested action.</param>
    public CloseDialogMessage(bool wasConfirmed)
        : base(wasConfirmed)
    {
    }
}
