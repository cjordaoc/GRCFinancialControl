using System;
using App.Presentation.Localization;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRC.Shared.UI.Messages;

namespace InvoicePlanner.Avalonia.ViewModels;

public sealed partial class InvoiceDescriptionPreviewViewModel : ViewModelBase
{
    private readonly RelayCommand _closeCommand;

    public InvoiceDescriptionPreviewViewModel(string header, string description, int sequence, int totalInvoices)
    {
        Header = header ?? throw new ArgumentNullException(nameof(header));
        Description = description ?? string.Empty;
        SequenceSummary = LocalizationRegistry.Format("INV_InvoicePlan_Preview_LineSummary", sequence, totalInvoices);
        _closeCommand = new RelayCommand(Close);
    }

    public string Header { get; }

    public string Description { get; }

    public string SequenceSummary { get; }

    public IRelayCommand CloseCommand => _closeCommand;

    private void Close()
    {
        Messenger.Send(new CloseDialogMessage(false));
    }
}
