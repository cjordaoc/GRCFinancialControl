using App.Presentation.Localization;
using GRC.Shared.UI.Services;
using System.Collections.ObjectModel;

namespace App.Presentation.Services;

/// <summary>
/// App-specific toast service that integrates LocalizationRegistry with shared ToastService.
/// Re-exports the shared Notifications collection for backward compatibility.
/// </summary>
public static class ToastService
{
    /// <summary>
    /// Observable collection of active toast notifications (from shared service).
    /// </summary>
    public static ReadOnlyObservableCollection<GRC.Shared.UI.Services.ToastNotification> Notifications => 
        GRC.Shared.UI.Services.ToastService.Notifications;

    public static void ShowSuccess(string resourceKey, params object[] arguments)
    {
        var message = ResolveMessage(resourceKey, arguments);
        GRC.Shared.UI.Services.ToastService.ShowSuccess(message);
    }

    public static void ShowWarning(string resourceKey, params object[] arguments)
    {
        var message = ResolveMessage(resourceKey, arguments);
        GRC.Shared.UI.Services.ToastService.ShowWarning(message);
    }

    public static void ShowError(string resourceKey, params object[] arguments)
    {
        var message = ResolveMessage(resourceKey, arguments);
        GRC.Shared.UI.Services.ToastService.ShowError(message);
    }

    private static string ResolveMessage(string resourceKey, params object[] arguments)
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
