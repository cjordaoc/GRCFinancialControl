using System;
using InvoicePlanner.Avalonia.Resources;

namespace InvoicePlanner.Avalonia.Services;

public static class ConnectionErrorMessageFormatter
{
    private const string MissingProviderFragment = "No database provider has been configured";

    public static string Format(Exception exception, string fallbackMessage)
    {
        if (exception is InvalidOperationException invalidOperation &&
            invalidOperation.Message.Contains(MissingProviderFragment, StringComparison.OrdinalIgnoreCase))
        {
            return Strings.Get("CommonConnectionMissing");
        }

        if (exception.InnerException is not null)
        {
            return Format(exception.InnerException, fallbackMessage);
        }

        return fallbackMessage;
    }
}
