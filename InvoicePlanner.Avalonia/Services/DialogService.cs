using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Messaging;
using GRC.Shared.UI.Dialogs;
using GRC.Shared.UI.Messages;
using InvoicePlanner.Avalonia.ViewModels;

namespace InvoicePlanner.Avalonia.Services
{
    public class DialogService
    {
        private readonly ViewLocator _viewLocator = new();
        private readonly Stack<Window> _dialogStack = new();
        private readonly IModalDialogService _modalDialogService;

        private Window? CurrentDialog => _dialogStack.Count > 0 ? _dialogStack.Peek() : null;

        public DialogService(IMessenger messenger, IModalDialogService modalDialogService)
        {
            _modalDialogService = modalDialogService;
            messenger.Register<CloseDialogMessage>(this, (r, m) =>
            {
                CurrentDialog?.Close(m.Value);
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
            var session = _modalDialogService.Create(owner, view, title, new ModalDialogOptions
            {
                Layout = ModalDialogLayout.OwnerAligned
            });
            var dialog = session.Dialog;

            dialog.Opened += (_, _) =>
            {
                Dispatcher.UIThread.Post(session.FocusFirstElement, DispatcherPriority.Background);
                dialog.KeyDown += session.KeyDownHandler;
            };

            var previousDialog = CurrentDialog;
            var focusScope = previousDialog ?? owner;
            var previousFocus = focusScope.FocusManager?.GetFocusedElement();

            if (previousDialog is null)
            {
                owner.IsEnabled = false;
            }
            else
            {
                previousDialog.IsEnabled = false;
            }

            _dialogStack.Push(dialog);

            try
            {
                var result = await dialog.ShowDialog<bool?>(owner);
                return result ?? false;
            }
            finally
            {
                if (_dialogStack.Count > 0 && ReferenceEquals(_dialogStack.Peek(), dialog))
                {
                    _dialogStack.Pop();
                }

                session.Dispose();
                dialog.KeyDown -= session.KeyDownHandler;

                var restoredDialog = CurrentDialog;

                if (restoredDialog is not null)
                {
                    restoredDialog.IsEnabled = true;
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (previousFocus is Control control)
                        {
                            control.Focus();
                        }
                        else
                        {
                            restoredDialog.Focus();
                        }
                    }, DispatcherPriority.Background);
                }
                else
                {
                    owner.IsEnabled = true;
                    Dispatcher.UIThread.Post(() => previousFocus?.Focus(), DispatcherPriority.Background);
                }
            }
        }
    }
}
