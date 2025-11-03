using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Messaging;
using GRC.Shared.UI.Messages;

namespace InvoicePlanner.Avalonia.Views;

public partial class InvoiceLinesEditorView : UserControl
{
    private bool _isRegistered;

    public InvoiceLinesEditorView()
    {
        InitializeComponent();

        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (_isRegistered)
        {
            return;
        }

        WeakReferenceMessenger.Default.Register<RefreshViewMessage>(this, OnRefreshRequested);
        _isRegistered = true;
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (!_isRegistered)
        {
            return;
        }

        WeakReferenceMessenger.Default.Unregister<RefreshViewMessage>(this);
        _isRegistered = false;
    }

    private void OnRefreshRequested(object recipient, RefreshViewMessage message)
    {
        if (!message.Matches(RefreshTargets.InvoiceLinesGrid))
        {
            return;
        }

        if (InvoiceLinesGrid is null)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            InvoiceLinesGrid.InvalidateMeasure();
            InvoiceLinesGrid.InvalidateArrange();
            InvoiceLinesGrid.UpdateLayout();

            var currentHeight = InvoiceLinesGrid.RowHeight;
            var baseHeight = double.IsNaN(currentHeight) ? 24d : currentHeight;
            var adjustedHeight = baseHeight + 0.1d;

            InvoiceLinesGrid.RowHeight = adjustedHeight;
            InvoiceLinesGrid.RowHeight = baseHeight;
        }, DispatcherPriority.Background);
    }
}
