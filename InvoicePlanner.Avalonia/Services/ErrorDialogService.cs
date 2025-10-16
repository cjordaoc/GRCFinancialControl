using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using InvoicePlanner.Avalonia.Resources;
using InvoicePlanner.Avalonia.ViewModels;
using InvoicePlanner.Avalonia.Views;
using Microsoft.Extensions.DependencyInjection;

namespace InvoicePlanner.Avalonia.Services;

public class ErrorDialogService : IErrorDialogService
{
    private readonly IServiceProvider _serviceProvider;

    public ErrorDialogService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public async Task ShowErrorAsync(Window? owner, string details, string? message = null)
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            using var scope = _serviceProvider.CreateScope();
            var viewModel = scope.ServiceProvider.GetRequiredService<ErrorDialogViewModel>();
            viewModel.Initialise(message ?? Strings.Get("ErrorDialogMessage"), details);

            var dialog = scope.ServiceProvider.GetRequiredService<ErrorDialog>();
            dialog.DataContext = viewModel;

            void OnOpened(object? sender, EventArgs args)
            {
                viewModel.Clipboard = dialog.Clipboard;
                dialog.Opened -= OnOpened;
            }

            dialog.Opened += OnOpened;

            TaskCompletionSource<object?> tcs = new();

            void Handler(object? sender, EventArgs args)
            {
                viewModel.Clipboard = null;
                viewModel.CloseRequested -= Handler;
                dialog.Closed -= ClosedHandler;
                tcs.TrySetResult(null);
            }

            void ClosedHandler(object? sender, EventArgs args)
            {
                viewModel.Clipboard = null;
                viewModel.CloseRequested -= Handler;
                dialog.Closed -= ClosedHandler;
                tcs.TrySetResult(null);
            }

            viewModel.CloseRequested += Handler;
            dialog.Closed += ClosedHandler;

            if (owner is not null && owner.IsVisible)
            {
                await dialog.ShowDialog(owner);
            }
            else
            {
                dialog.Show();
            }

            await tcs.Task;
        });
    }
}
