using System;
using App.Presentation.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using InvoicePlanner.Avalonia.Services;

namespace InvoicePlanner.Avalonia.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    public void ConfigureModalOverlay(IModalOverlayService modalOverlayService, IErrorDialogService errorDialogService)
    {
        if (Design.IsDesignMode)
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(modalOverlayService);
        ArgumentNullException.ThrowIfNull(errorDialogService);

        var overlayHost = this.FindControl<ModalOverlayHost>("OverlayHost")
            ?? throw new InvalidOperationException("Overlay host control was not found in the main window.");
        overlayHost.CloseRequested += (_, args) => modalOverlayService.Close(args.Result);
        modalOverlayService.AttachHost(overlayHost);
        errorDialogService.AttachHost(overlayHost);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
