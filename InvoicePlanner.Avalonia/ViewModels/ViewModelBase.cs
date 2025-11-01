using App.Presentation.Localization;
using CommunityToolkit.Mvvm.Messaging;
using GRC.Shared.UI.ViewModels;
using Invoices.Data.Repositories;

namespace InvoicePlanner.Avalonia.ViewModels;

/// <summary>
/// Provides shared helper behavior for Invoice Planner view models.
/// </summary>
public abstract class ViewModelBase : ObservableViewModelBase
{
    private const string UnknownLoginResourceKey = "INV_Access_Message_LoginUnknown";

    /// <summary>
    /// Initializes a new instance of the <see cref="ViewModelBase"/> class.
    /// </summary>
    protected ViewModelBase()
        : this(WeakReferenceMessenger.Default)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ViewModelBase"/> class.
    /// </summary>
    /// <param name="messenger">The messenger that handles cross-component communication.</param>
    protected ViewModelBase(IMessenger? messenger)
        : base(messenger)
    {
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
