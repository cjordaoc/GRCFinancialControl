using System;
using System.Threading.Tasks;
using Avalonia.Input.Platform;
using App.Presentation.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRC.Shared.UI.Messages;
using GRC.Shared.UI.ViewModels.Dialogs;

namespace InvoicePlanner.Avalonia.ViewModels;

public partial class ErrorDialogViewModel : InformationDialogViewModelBase
{
    internal IClipboard? Clipboard { get; set; }

    public IAsyncRelayCommand CopyDetailsCommand { get; }

    public ErrorDialogViewModel()
    {
        CopyDetailsCommand = new AsyncRelayCommand(CopyDetailsAsync);
        SecondaryCommand = CopyDetailsCommand;
        SecondaryButtonText = LocalizationRegistry.Get("INV_Button_CopyDetails");

        Title = LocalizationRegistry.Get("INV_Dialogs_Error_Title");
        Message = LocalizationRegistry.Get("INV_Dialogs_Error_Message");
        DetailsHeaderText = LocalizationRegistry.Get("INV_Dialogs_Error_Label_Details");
        DismissButtonText = LocalizationRegistry.Get("Global_Button_OK");

        OnDismissed = () => WeakReferenceMessenger.Default.Send(new CloseDialogMessage(true));
    }

    public void Initialise(string? messageText, string? detailsText)
    {
        if (!string.IsNullOrWhiteSpace(messageText))
        {
            Message = messageText!;
        }

        Details = detailsText ?? string.Empty;
    }

    private async Task CopyDetailsAsync()
    {
        if (Clipboard is null)
        {
            return;
        }

        await Clipboard.SetTextAsync(Details ?? string.Empty);
    }
}
