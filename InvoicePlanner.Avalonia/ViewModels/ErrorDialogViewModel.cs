using System;
using System.Threading.Tasks;
using Avalonia.Input.Platform;
using App.Presentation.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRC.Shared.UI.Messages;

namespace InvoicePlanner.Avalonia.ViewModels;

public partial class ErrorDialogViewModel : ViewModelBase
{
    public event EventHandler? CloseRequested;

    [ObservableProperty]
    private string title = LocalizationRegistry.Get("INV_Dialogs_Error_Title");

    [ObservableProperty]
    private string message = LocalizationRegistry.Get("INV_Dialogs_Error_Message");

    [ObservableProperty]
    private string details = string.Empty;

    [ObservableProperty]
    private string detailsLabel = LocalizationRegistry.Get("INV_Dialogs_Error_Label_Details");

    internal IClipboard? Clipboard { get; set; }

    public IAsyncRelayCommand CopyDetailsCommand { get; }

    public IRelayCommand SaveCommand { get; }

    public IRelayCommand CloseCommand { get; }

    public ErrorDialogViewModel()
    {
        CopyDetailsCommand = new AsyncRelayCommand(CopyDetailsAsync);
        SaveCommand = new RelayCommand(OnSave);
        CloseCommand = new RelayCommand(OnCancel);
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

    private void OnSave()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
        Messenger.Send(new CloseDialogMessage(true));
    }

    private void OnCancel()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
        Messenger.Send(new CloseDialogMessage(false));
    }
}
