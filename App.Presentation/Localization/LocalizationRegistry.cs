using System;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Resources;
using Avalonia.Data;
using Avalonia.Markup.Xaml;

namespace App.Presentation.Localization;

public interface ILocalizationProvider
{
    string Get(string key);

    string Format(string key, params object[] arguments);
}

public sealed class ResourceManagerLocalizationProvider : ILocalizationProvider
{
    private readonly ResourceManager _resourceManager;

    public ResourceManagerLocalizationProvider(string baseName, Assembly assembly)
    {
        if (string.IsNullOrWhiteSpace(baseName))
        {
            throw new ArgumentException("Base name is required.", nameof(baseName));
        }

        _resourceManager = new ResourceManager(baseName, assembly ?? throw new ArgumentNullException(nameof(assembly)));
    }

    public string Get(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        return _resourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
    }

    public string Format(string key, params object[] arguments)
    {
        var format = Get(key);
        return string.Format(CultureInfo.CurrentUICulture, format, arguments);
    }
}

public static class LocalizationRegistry
{
    private static ILocalizationProvider _provider = new NullLocalizationProvider();
    private static readonly LocalizationBindingSource BindingSource = new();

    public static event EventHandler? CultureChanged;

    public static void Configure(ILocalizationProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        NotifyCultureChanged();
    }

    public static string Get(string key)
    {
        return _provider.Get(key);
    }

    public static string Format(string key, params object[] arguments)
    {
        return _provider.Format(key, arguments);
    }

    internal static LocalizationBindingSource GetBindingSource()
    {
        return BindingSource;
    }

    internal static void NotifyCultureChanged()
    {
        CultureChanged?.Invoke(null, EventArgs.Empty);
        BindingSource.RaiseChanged();
    }

    private sealed class NullLocalizationProvider : ILocalizationProvider
    {
        public string Get(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            return key;
        }

        public string Format(string key, params object[] arguments)
        {
            var format = Get(key);

            if (arguments is { Length: > 0 })
            {
                return string.Format(CultureInfo.InvariantCulture, format, arguments);
            }

            return format;
        }
    }
}

public sealed class LocExtension : MarkupExtension
{
    public string? Key { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return new Binding
        {
            Source = LocalizationRegistry.GetBindingSource(),
            Path = $"[{Key ?? string.Empty}]",
            Mode = BindingMode.OneWay,
            FallbackValue = Key ?? string.Empty
        };
    }
}

internal sealed class LocalizationBindingSource : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string this[string key] => LocalizationRegistry.Get(key);

    public void RaiseChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }
}
