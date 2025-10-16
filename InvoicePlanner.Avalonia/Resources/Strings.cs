using System;
using System.Globalization;
using System.Resources;
using Avalonia.Markup.Xaml;

namespace InvoicePlanner.Avalonia.Resources;

public static class Strings
{
    private static readonly ResourceManager ResourceManager = new("InvoicePlanner.Avalonia.Resources.Strings", typeof(Strings).Assembly);

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

public class LocExtension : MarkupExtension
{
    public string? Key { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (string.IsNullOrWhiteSpace(Key))
        {
            return string.Empty;
        }

        return Strings.Get(Key);
    }
}
