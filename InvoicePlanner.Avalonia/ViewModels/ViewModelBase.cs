using App.Presentation.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Invoices.Data.Repositories;

namespace InvoicePlanner.Avalonia.ViewModels;

public abstract partial class ViewModelBase : ObservableObject
{
    protected IMessenger Messenger { get; }

    protected ViewModelBase()
        : this(WeakReferenceMessenger.Default)
    {
    }

    protected ViewModelBase(IMessenger messenger)
    {
        Messenger = messenger ?? WeakReferenceMessenger.Default;
    }

    protected static string GetLoginDisplay(IInvoiceAccessScope accessScope)
    {
        if (accessScope is null)
        {
            return LocalizationRegistry.Get("Access.Message.LoginUnknown");
        }

        var login = accessScope.Login;
        return string.IsNullOrWhiteSpace(login)
            ? LocalizationRegistry.Get("Access.Message.LoginUnknown")
            : login;
    }
}
