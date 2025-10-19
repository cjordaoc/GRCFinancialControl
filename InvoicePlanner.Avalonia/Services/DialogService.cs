using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.Messaging;
using InvoicePlanner.Avalonia.Messages;
using InvoicePlanner.Avalonia.Services.Interfaces;
using InvoicePlanner.Avalonia.ViewModels;

namespace InvoicePlanner.Avalonia.Services
{
    public class DialogService : IDialogService
    {
        private readonly ViewLocator _viewLocator = new();
        private Window? _currentDialog;

        public DialogService(IWeakReferenceMessenger messenger)
        {
            messenger.Register<CloseDialogMessage>(this, (r, m) =>
            {
                _currentDialog?.Close(m.Value);
            });
        }

        public async Task<bool> ShowDialogAsync(ViewModelBase viewModel, string? title = null)
        {
            if (App.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            {
                return false;
            }

            if (_viewLocator.Build(viewModel) is not UserControl view)
            {
                throw new InvalidOperationException($"Could not locate a view for the view model '{viewModel.GetType().FullName}'.");
            }

            view.DataContext = viewModel;

            _currentDialog = new Window
            {
                Title = title,
                Content = view,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SizeToContent = SizeToContent.WidthAndHeight
            };

            if (desktop.MainWindow is null)
            {
                return false;
            }

            var result = await _currentDialog.ShowDialog<bool?>(desktop.MainWindow);
            return result ?? false;
        }
    }
}
