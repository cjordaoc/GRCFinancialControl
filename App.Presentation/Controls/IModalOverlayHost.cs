using System;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace App.Presentation.Controls;

public interface IModalOverlayHost
{
    event EventHandler<ModalOverlayCloseRequestedEventArgs>? CloseRequested;

    object? OverlayContent { get; set; }

    string? Title { get; set; }

    bool CanClose { get; set; }

    bool IsOverlayOpen { get; }

    Task<bool?> ShowModalAsync(UserControl content, string? title = null, bool canClose = true);

    void Close(bool? result = null);
}
