using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Resources;
using Avalonia.Data;
using Avalonia.Markup.Xaml;

namespace App.Presentation.Localization;

[SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "Localized resource lookup follows Get(key) convention.")]
public interface ILocalizationProvider
{
    string Get(string key);

    string Format(string key, params object[] arguments);
}

public sealed class ResourceManagerLocalizationProvider : ILocalizationProvider
{
    private readonly ResourceManager _resourceManager;

    public ResourceManagerLocalizationProvider(string baseName, Assembly assembly)
        : this(CreateResourceManager(baseName, assembly))
    {
    }

    public ResourceManagerLocalizationProvider(ResourceManager resourceManager)
    {
        _resourceManager = resourceManager ?? throw new ArgumentNullException(nameof(resourceManager));
    }

    private static ResourceManager CreateResourceManager(string baseName, Assembly assembly)
    {
        if (string.IsNullOrWhiteSpace(baseName))
        {
            throw new ArgumentException("Base name is required.", nameof(baseName));
        }

        ArgumentNullException.ThrowIfNull(assembly);

        return new ResourceManager(baseName, assembly);
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
        return string.Format(CultureInfo.CurrentCulture, format, arguments);
    }
}

public sealed class CompositeLocalizationProvider : ILocalizationProvider
{
    private readonly IReadOnlyList<ILocalizationProvider> _providers;

    public CompositeLocalizationProvider(params ILocalizationProvider[] providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        var activeProviders = new List<ILocalizationProvider>();
        foreach (var provider in providers)
        {
            if (provider is not null)
            {
                activeProviders.Add(provider);
            }
        }

        _providers = activeProviders;
    }

    public string Get(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        foreach (var provider in _providers)
        {
            var value = provider.Get(key);
            if (!string.Equals(value, key, StringComparison.Ordinal))
            {
                return value;
            }
        }

        return key;
    }

    public string Format(string key, params object[] arguments)
    {
        var format = Get(key);
        return string.Format(CultureInfo.CurrentCulture, format, arguments);
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
