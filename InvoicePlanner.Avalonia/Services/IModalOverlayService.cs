using System.Threading.Tasks;
using App.Presentation.Controls;

namespace InvoicePlanner.Avalonia.Services;

public interface IModalOverlayService
{
    Task<bool?> ShowAsync(object content, string? title = null, bool canClose = true);
    void Close(bool? result = null);
    void AttachHost(IModalOverlayHost host);
}
