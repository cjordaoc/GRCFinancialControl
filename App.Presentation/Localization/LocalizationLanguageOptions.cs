using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace App.Presentation.Localization;

public sealed class LanguageOption : INotifyPropertyChanged
{
    public LanguageOption(string cultureName, string displayKey)
    {
        CultureName = cultureName;
        DisplayKey = displayKey;
        LocalizationRegistry.CultureChanged += HandleCultureChanged;
    }

    public string CultureName { get; }

    public string DisplayKey { get; }

    public string DisplayName => LocalizationRegistry.Get(DisplayKey);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void HandleCultureChanged(object? sender, EventArgs e)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
    }
}

public static class LocalizationLanguageOptions
{
    private static readonly (string CultureName, string DisplayKey)[] Definitions =
    {
        ("en-US", "Global_Localization_Language_English"),
        ("pt-BR", "Global_Localization_Language_Portuguese")
    };

    public static IReadOnlyList<LanguageOption> Create()
    {
        return Definitions
            .Select(definition => new LanguageOption(definition.CultureName, definition.DisplayKey))
            .ToList();
    }
}
