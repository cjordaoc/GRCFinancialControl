using System.Threading.Tasks;
using GRCFinancialControl.Avalonia.ViewModels;

namespace GRCFinancialControl.Avalonia.Services.Interfaces;

/// <summary>
/// Defines the contract for displaying modal dialogs in the GRC Financial Control application.
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Displays a modal dialog for the given view model.
    /// </summary>
    /// <param name="viewModel">The view model that drives the dialog content.</param>
    /// <param name="title">Optional title for the dialog window.</param>
    /// <returns>A task that completes when the dialog is closed.</returns>
    Task<bool> ShowDialogAsync(ViewModelBase viewModel, string? title = null);

    /// <summary>
    /// Displays a confirmation dialog with Yes/No buttons.
    /// </summary>
    /// <param name="title">The dialog title.</param>
    /// <param name="message">The confirmation message to display.</param>
    /// <returns>A task that returns <c>true</c> if the user clicked Yes; otherwise <c>false</c>.</returns>
    Task<bool> ShowConfirmationAsync(string title, string message);
}
