using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using App.Presentation.Controls;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Messaging;
using InvoicePlanner.Avalonia.Messages;

namespace InvoicePlanner.Avalonia.Services;

public sealed class ModalOverlayService : IModalOverlayService
{
    private IModalOverlayHost? _overlayHost;

    public Task<bool?> ShowAsync(object content, string? title = null, bool canClose = true)
    {
        if (_overlayHost is null)
        {
            throw new InvalidOperationException("The overlay host has not been attached. Please ensure AttachHost is called before showing an overlay.");
        }

        if (content is not UserControl view)
        {
            throw new InvalidOperationException($"The content must be a UserControl, but was '{content.GetType().FullName}'.");
        }

        return _overlayHost.ShowModalAsync(view, title, canClose);
    }

    public void Close(bool? result = null)
    {
        _overlayHost?.Close(result);
    }

    public void AttachHost(IModalOverlayHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        _overlayHost = host;
    }
}
