namespace GRC.Shared.UI.Messages;

public sealed class ApplicationRestartRequestedMessage
{
    private ApplicationRestartRequestedMessage()
    {
    }

    public static ApplicationRestartRequestedMessage Instance { get; } = new();
}

