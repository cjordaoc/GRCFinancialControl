using App.Presentation.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Invoices.Data.Repositories;

namespace InvoicePlanner.Avalonia.ViewModels;

/// <summary>
/// Provides shared helper behavior for Invoice Planner view models.
/// </summary>
public abstract class ViewModelBase : ObservableObject
{
    private const string UnknownLoginResourceKey = "Access.Message.LoginUnknown";

    /// <summary>
    /// Gets the messenger used to communicate with other view models.
    /// </summary>
    protected IMessenger Messenger { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ViewModelBase"/> class with the default messenger.
    /// </summary>
    protected ViewModelBase()
        : this(WeakReferenceMessenger.Default)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ViewModelBase"/> class.
    /// </summary>
    /// <param name="messenger">The messenger that handles cross-component communication. When <c>null</c>, the default messenger is used.</param>
    protected ViewModelBase(IMessenger? messenger)
    {
        Messenger = messenger ?? WeakReferenceMessenger.Default;
    }

    /// <summary>
    /// Builds the login display string for the provided access scope.
    /// </summary>
    /// <param name="accessScope">The access scope describing the authenticated user.</param>
    /// <returns>A localized string representing the login.</returns>
    protected static string GetLoginDisplay(IInvoiceAccessScope accessScope)
    {
        if (accessScope is null)
        {
            return LocalizationRegistry.Get(UnknownLoginResourceKey);
        }

        string? login = accessScope.Login;
        return string.IsNullOrWhiteSpace(login)
            ? LocalizationRegistry.Get(UnknownLoginResourceKey)
            : login;
    }
}
