namespace InvoicePlanner.Avalonia.Messages;

public sealed class ConnectionSettingsImportedMessage
{
    private ConnectionSettingsImportedMessage()
    {
    }

    public static ConnectionSettingsImportedMessage Instance { get; } = new();
}
