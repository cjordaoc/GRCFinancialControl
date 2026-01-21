namespace GRCFinancialControl.Avalonia.Services.Interfaces;

/// <summary>
/// Defines the contract for presenter-specific UI services that display notifications and feedback.
/// This interface abstracts UI presentation logic from business logic, enabling better testability
/// and separation of concerns.
/// </summary>
public interface IPresenterService
{
    /// <summary>
    /// Displays a success toast notification with optional parameters.
    /// </summary>
    /// <param name="localizationKey">The localization resource key for the message.</param>
    /// <param name="formatArgs">Optional parameters to format into the localized message.</param>
    void ShowSuccess(string localizationKey, params object?[] formatArgs);

    /// <summary>
    /// Displays a warning toast notification with optional parameters.
    /// </summary>
    /// <param name="localizationKey">The localization resource key for the message.</param>
    /// <param name="formatArgs">Optional parameters to format into the localized message.</param>
    void ShowWarning(string localizationKey, params object?[] formatArgs);

    /// <summary>
    /// Displays an error toast notification with optional parameters.
    /// </summary>
    /// <param name="localizationKey">The localization resource key for the message.</param>
    /// <param name="formatArgs">Optional parameters to format into the localized message.</param>
    void ShowError(string localizationKey, params object?[] formatArgs);

    /// <summary>
    /// Logs an informational message for diagnostics.
    /// </summary>
    /// <param name="message">The message to log.</param>
    void LogInfo(string message);

    /// <summary>
    /// Logs a warning message for diagnostics.
    /// </summary>
    /// <param name="message">The message to log.</param>
    void LogWarning(string message);

    /// <summary>
    /// Logs an error message for diagnostics.
    /// </summary>
    /// <param name="message">The message to log.</param>
    void LogError(string message);
}
