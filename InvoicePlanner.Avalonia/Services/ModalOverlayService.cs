using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using App.Presentation.Controls;
using CommunityToolkit.Mvvm.Messaging;
using InvoicePlanner.Avalonia.Messages;

namespace InvoicePlanner.Avalonia.Services;

public sealed class ModalOverlayService : IModalOverlayService, IRecipient<CloseModalOverlayMessage>
{
    private readonly IMessenger _messenger;
    private readonly Stack<TaskCompletionSource<bool?>> _overlayStack = new();
    private IModalOverlayHost? _overlayHost;

    public ModalOverlayService(IMessenger messenger)
    {
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        _messenger.Register<CloseModalOverlayMessage>(this);
    }

    public Task<bool?> ShowAsync(object content, string? title = null, bool canClose = true)
    {
        ArgumentNullException.ThrowIfNull(content);

        var tcs = new TaskCompletionSource<bool?>();
        _overlayStack.Push(tcs);
        _messenger.Send(new OpenModalOverlayMessage(content, title, canClose));
        return tcs.Task;
    }

    public void Close(bool? result = null)
    {
        _messenger.Send(new CloseModalOverlayMessage(result));
    }

    public void AttachHost(IModalOverlayHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        _overlayHost = host;
    }

    public void Receive(CloseModalOverlayMessage message)
    {
        if (_overlayStack.TryPop(out var tcs))
        {
            tcs.TrySetResult(message.Value);
        }

        if (_overlayHost?.IsOverlayOpen == true)
        {
            _overlayHost.Close(message.Value);
        }
    }
}
