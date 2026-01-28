using App.Presentation.Localization;
using GRC.Shared.UI.Services;

namespace App.Presentation.Services;

/// <summary>
/// App-specific wrapper around GRC.Shared.UI.ToastService that integrates LocalizationRegistry.
/// Enables passing localization keys directly instead of pre-formatted messages.
/// </summary>
public static class ToastService
{
    /// <summary>
    /// Displays a success toast notification with localization support.
    /// </summary>
    public static void ShowSuccess(string resourceKey, params object[] arguments)
    {
        var message = FormatMessage(resourceKey, arguments);
        GRC.Shared.UI.Services.ToastService.ShowSuccess(message);
    }

    /// <summary>
    /// Displays a warning toast notification with localization support.
    /// </summary>
    public static void ShowWarning(string resourceKey, params object[] arguments)
    {
        var message = FormatMessage(resourceKey, arguments);
        GRC.Shared.UI.Services.ToastService.ShowWarning(message);
    }

    /// <summary>
    /// Displays an error toast notification with localization support.
    /// </summary>
    public static void ShowError(string resourceKey, params object[] arguments)
    {
        var message = FormatMessage(resourceKey, arguments);
        GRC.Shared.UI.Services.ToastService.ShowError(message);
    }

    private static string FormatMessage(string resourceKey, params object[] arguments)
    {
        if (string.IsNullOrWhiteSpace(resourceKey))
        {
            return string.Empty;
        }

        return arguments is { Length: > 0 }
            ? LocalizationRegistry.Format(resourceKey, arguments)
            : LocalizationRegistry.Get(resourceKey);
    }
}
