using System;
using System.Threading.Tasks;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InvoicePlanner.Avalonia.Resources;

namespace InvoicePlanner.Avalonia.ViewModels;

public partial class ErrorDialogViewModel : ObservableObject
{
    public event EventHandler? CloseRequested;

    [ObservableProperty]
    private string title = Strings.Get("ErrorDialogTitle");

    [ObservableProperty]
    private string message = Strings.Get("ErrorDialogMessage");

    [ObservableProperty]
    private string details = string.Empty;

    [ObservableProperty]
    private string detailsLabel = Strings.Get("ErrorDialogDetailsLabel");

    internal IClipboard? Clipboard { get; set; }

    public IAsyncRelayCommand CopyDetailsCommand { get; }

    public IRelayCommand CloseCommand { get; }

    public ErrorDialogViewModel()
    {
        CopyDetailsCommand = new AsyncRelayCommand(CopyDetailsAsync);
        CloseCommand = new RelayCommand(() => CloseRequested?.Invoke(this, EventArgs.Empty));
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
