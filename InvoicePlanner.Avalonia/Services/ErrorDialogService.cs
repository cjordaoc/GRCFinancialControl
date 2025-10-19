using System;
using System.Threading.Tasks;
using App.Presentation.Controls;
using App.Presentation.Localization;
using Avalonia.Controls;
using Avalonia.Threading;
using InvoicePlanner.Avalonia.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace InvoicePlanner.Avalonia.Services;

public class ErrorDialogService : IErrorDialogService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IModalOverlayService _modalOverlayService;

    public ErrorDialogService(IServiceProvider serviceProvider, IModalOverlayService modalOverlayService)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _modalOverlayService = modalOverlayService ?? throw new ArgumentNullException(nameof(modalOverlayService));
    }

    public async Task ShowErrorAsync(Window? owner, string details, string? message = null)
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            using var scope = _serviceProvider.CreateScope();
            var viewModel = scope.ServiceProvider.GetRequiredService<ErrorDialogViewModel>();
            viewModel.Initialise(message ?? LocalizationRegistry.Get("Dialogs.Error.Message"), details);

            if (owner is not null && owner.IsVisible)
            {
                viewModel.Clipboard = owner.Clipboard;
            }

            void Handler(object? sender, EventArgs args)
            {
                _modalOverlayService.Close(false);
            }

            viewModel.CloseRequested += Handler;

            try
            {
                await _modalOverlayService.ShowAsync(viewModel, viewModel.Title);
            }
            finally
            {
                viewModel.CloseRequested -= Handler;
                viewModel.Clipboard = null;
            }
        });
    }

    public void AttachHost(IModalOverlayHost host)
    {
        _modalOverlayService.AttachHost(host);
    }
}
