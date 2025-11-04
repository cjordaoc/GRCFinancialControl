using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.ViewModels;
using GRCFinancialControl.Avalonia.ViewModels.Dialogs;
using GRC.Shared.UI.Dialogs;
using GRC.Shared.UI.Messages;

namespace GRCFinancialControl.Avalonia.Services
{
    public class DialogService
    {
        private readonly IMessenger _messenger;
        private readonly ViewLocator _viewLocator = new();
        private readonly IModalDialogService _modalDialogService;
        private readonly Stack<Window> _dialogStack = new();

        private Window? CurrentDialog => _dialogStack.Count > 0 ? _dialogStack.Peek() : null;

        public DialogService(IMessenger messenger, IModalDialogService modalDialogService)
        {
            _messenger = messenger;
            _modalDialogService = modalDialogService;
            _messenger.Register<CloseDialogMessage>(this, (recipient, message) =>
            {
                CurrentDialog?.Close(message.Value);
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

            if (desktop.MainWindow is null)
            {
                return false;
            }

            var owner = desktop.MainWindow;
            var session = _modalDialogService.Create(owner, view, title);
            var dialog = session.Dialog;
            _dialogStack.Push(dialog);

            dialog.Opened += (_, _) =>
            {
                Dispatcher.UIThread.Post(session.FocusFirstElement, DispatcherPriority.Background);
                dialog.KeyDown += session.KeyDownHandler;
            };

            var previousFocus = owner.FocusManager?.GetFocusedElement();

            try
            {
                owner.IsEnabled = false;
                var result = await dialog.ShowDialog<bool?>(owner);
                return result ?? false;
            }
            finally
            {
                owner.IsEnabled = true;
                session.Dispose();

                if (dialog is not null)
                {
                    dialog.KeyDown -= session.KeyDownHandler;
                }

                if (_dialogStack.Count > 0 && _dialogStack.Peek() == dialog)
                {
                    _dialogStack.Pop();
                }

                Dispatcher.UIThread.Post(() => previousFocus?.Focus(), DispatcherPriority.Background);
            }
        }

        public Task<bool> ShowConfirmationAsync(string title, string message)
        {
            var confirmationViewModel = new ConfirmationDialogViewModel(title, message, _messenger);
            return ShowDialogAsync(confirmationViewModel, title);
        }

    }
}
