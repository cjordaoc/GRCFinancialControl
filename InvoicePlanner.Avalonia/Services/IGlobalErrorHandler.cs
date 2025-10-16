using Avalonia.Controls.ApplicationLifetimes;

namespace InvoicePlanner.Avalonia.Services;

public interface IGlobalErrorHandler
{
    void Register(IClassicDesktopStyleApplicationLifetime lifetime);
}
