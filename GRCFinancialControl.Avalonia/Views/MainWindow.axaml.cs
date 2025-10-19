using System;
using App.Presentation.Controls;
using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;
using GRCFinancialControl.Avalonia.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.Views
{
    public partial class MainWindow : Window
    {
        private IMessenger? _messenger;

        public MainWindow()
        {
            InitializeComponent();
        }

        public void ConfigureModalOverlay(IMessenger messenger, IDialogService dialogService)
        {
            if (Design.IsDesignMode)
            {
                return;
            }

            ArgumentNullException.ThrowIfNull(messenger);
            ArgumentNullException.ThrowIfNull(dialogService);

            _messenger = messenger;
            var overlayHost = this.FindControl<ModalOverlayHost>("OverlayHost")
                ?? throw new InvalidOperationException("Overlay host control was not found in the main window.");
            overlayHost.CloseRequested += OnOverlayCloseRequested;
            dialogService.AttachHost(overlayHost);
        }

        private void OnOverlayCloseRequested(object? sender, ModalOverlayCloseRequestedEventArgs e)
        {
            _messenger?.Send(new CloseDialogMessage(e.Result));
        }
    }
}
