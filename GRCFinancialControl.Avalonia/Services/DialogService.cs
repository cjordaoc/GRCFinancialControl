using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Services.Interfaces;
using GRCFinancialControl.Avalonia.ViewModels;
using GRCFinancialControl.Avalonia.ViewModels.Dialogs;
using GRCFinancialControl.Avalonia.Views.Dialogs;

namespace GRCFinancialControl.Avalonia.Services
{
    public class DialogService : IDialogService
    {
        private readonly IMessenger _messenger;
        private readonly ViewLocator _viewLocator = new();

        public DialogService(IMessenger messenger)
        {
            _messenger = messenger;
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

            view.HorizontalAlignment = HorizontalAlignment.Stretch;
            view.VerticalAlignment = VerticalAlignment.Stretch;
            view.DataContext = viewModel;

            var dialog = new DialogWindow
            {
                Title = title,
                Content = view
            };

            if (desktop.MainWindow is null)
            {
                return false;
            }

            var result = await dialog.ShowDialog<bool?>(desktop.MainWindow);
            return result ?? false;
        }

        public Task<bool> ShowConfirmationAsync(string title, string message)
        {
            var confirmationViewModel = new ConfirmationDialogViewModel(title, message, _messenger);
            return ShowDialogAsync(confirmationViewModel, title);
        }
    }
}
