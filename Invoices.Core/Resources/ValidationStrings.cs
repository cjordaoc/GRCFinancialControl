using System;
using System.Globalization;
using System.Resources;

namespace Invoices.Core.Resources;

public static class ValidationStrings
{
    private static readonly ResourceManager ResourceManager = new("Invoices.Core.Resources.ValidationStrings", typeof(ValidationStrings).Assembly);

    public static string Get(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key is required.", nameof(key));
        }

        return ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
    }

    public static string Format(string key, params object[] arguments)
    {
        var format = Get(key);
        return string.Format(CultureInfo.CurrentUICulture, format, arguments);
    }
}
