using System;
using App.Presentation.Localization;

namespace InvoicePlanner.Avalonia.Services;

/// <summary>
/// Formats database connection error messages with localization support.
/// </summary>
public static class ConnectionErrorMessageFormatter
{
    private const string MissingProviderFragment = "No database provider has been configured";

    public static string Format(Exception exception, string fallbackMessage)
    {
        if (exception is InvalidOperationException invalidOperation &&
            invalidOperation.Message.Contains(MissingProviderFragment, StringComparison.OrdinalIgnoreCase))
        {
            return LocalizationRegistry.Get("INV_Connection_Message_MissingSettings");
        }

        if (exception.InnerException is not null)
        {
            return Format(exception.InnerException, fallbackMessage);
        }

        return fallbackMessage;
    }
}
