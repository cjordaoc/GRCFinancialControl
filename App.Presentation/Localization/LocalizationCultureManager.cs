using System.Globalization;

namespace App.Presentation.Localization;

public static class LocalizationCultureManager
{
    private const string DefaultCultureName = "en-US";

    public static void ApplyCulture(string? cultureName)
    {
        var name = string.IsNullOrWhiteSpace(cultureName)
            ? DefaultCultureName
            : cultureName;

        CultureInfo culture;

        try
        {
            culture = CultureInfo.GetCultureInfo(name);
        }
        catch (CultureNotFoundException)
        {
            culture = CultureInfo.GetCultureInfo(DefaultCultureName);
        }

        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        LocalizationRegistry.NotifyCultureChanged();
    }
}
