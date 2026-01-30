using System;
using System.Linq;
using App.Presentation.Localization;
using GRC.Shared.UI.Services;
using GRCFinancialControl.Avalonia.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.Services;

/// <summary>
/// Adapter implementation of IPresenterService that delegates to existing UI services.
/// Provides a unified interface for presenter-specific operations (toasts and logging).
/// </summary>
public sealed class PresenterService : IPresenterService
{
    private readonly LoggingService _loggingService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PresenterService"/> class.
    /// </summary>
    /// <param name="loggingService">The logging service for diagnostic messages.</param>
    public PresenterService(LoggingService loggingService)
    {
        _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
    }

    public void ShowSuccess(string localizationKey, params object?[] formatArgs)
    {
        var message = LocalizationRegistry.Format(localizationKey, formatArgs.Where(x => x != null).Cast<object>().ToArray());
        ToastService.ShowSuccess(message);
    }

    public void ShowWarning(string localizationKey, params object?[] formatArgs)
    {
        var message = LocalizationRegistry.Format(localizationKey, formatArgs.Where(x => x != null).Cast<object>().ToArray());
        ToastService.ShowWarning(message);
    }

    public void ShowError(string localizationKey, params object?[] formatArgs)
    {
        var message = LocalizationRegistry.Format(localizationKey, formatArgs.Where(x => x != null).Cast<object>().ToArray());
        ToastService.ShowError(message);
    }

    public void LogInfo(string message)
    {
        _loggingService.LogInfo(message);
    }

    public void LogWarning(string message)
    {
        _loggingService.LogWarning(message);
    }

    public void LogError(string message)
    {
        _loggingService.LogError(message);
    }
}
