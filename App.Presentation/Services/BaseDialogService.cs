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

namespace App.Presentation.Services;

/// <summary>
/// Base implementation for modal dialog orchestration with focus management and dialog stacking.
/// Derived classes can customize modal options and nested dialog behavior.
/// </summary>
public abstract class BaseDialogService
{
    private readonly Stack<Window> _dialogStack = new();
    private readonly IModalDialogService _modalDialogService;

    /// <summary>
    /// Gets the currently active dialog, or null if stack is empty.
    /// </summary>
    protected Window? CurrentDialog => _dialogStack.Count > 0 ? _dialogStack.Peek() : null;

    /// <summary>
    /// Initializes base dialog service with message handling for CloseDialogMessage.
    /// </summary>
    protected BaseDialogService(IMessenger messenger, IModalDialogService modalDialogService)
    {
        _modalDialogService = modalDialogService;
        messenger.Register<CloseDialogMessage>(this, (recipient, message) =>
        {
            CurrentDialog?.Close(message.Value);
        });
    }

    /// <summary>
    /// Displays a modal dialog for the given view model.
    /// </summary>
    public async Task<bool> ShowDialogAsync(object viewModel, string? title = null)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return false;
        }

        if (desktop.MainWindow is null)
        {
            return false;
        }

        var view = BuildView(viewModel);
        if (view is null)
        {
            throw new InvalidOperationException($"Could not locate a view for the view model '{viewModel.GetType().FullName}'.");
        }

        view.DataContext = viewModel;

        var owner = desktop.MainWindow;
        var options = GetModalDialogOptions();
        var session = _modalDialogService.Create(owner, view, title, options);
        var dialog = session.Dialog;

        dialog.Opened += (_, _) =>
        {
            Dispatcher.UIThread.Post(session.FocusFirstElement, DispatcherPriority.Background);
            dialog.KeyDown += session.KeyDownHandler;
        };

        var previousDialog = CurrentDialog;
        _dialogStack.Push(dialog);
        var focusState = OnDialogOpening(dialog, owner, previousDialog);

        try
        {
            var result = await dialog.ShowDialog<bool?>(owner).ConfigureAwait(false);
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

            OnDialogClosing(dialog, owner, focusState);
        }
    }

    /// <summary>
    /// Builds the view for the given view model using the view locator.
    /// </summary>
    protected abstract UserControl? BuildView(object viewModel);

    /// <summary>
    /// Returns modal dialog options for session creation.
    /// Override to customize layout and sizing behavior.
    /// </summary>
    protected virtual ModalDialogOptions? GetModalDialogOptions() => null;

    /// <summary>
    /// Called after dialog is pushed to stack but before showing.
    /// Handles owner/previous dialog disabling and focus capture.
    /// </summary>
    /// <param name="dialog">The dialog about to be shown.</param>
    /// <param name="owner">The main window owner.</param>
    /// <param name="previousDialog">The previously active dialog, or null if this is top-level.</param>
    /// <returns>State for restoration in OnDialogClosing.</returns>
    protected virtual DialogFocusState OnDialogOpening(Window dialog, Window owner, Window? previousDialog)
    {
        var previousFocus = owner.FocusManager?.GetFocusedElement();

        owner.IsEnabled = false;

        return new DialogFocusState(previousDialog, previousFocus);
    }

    /// <summary>
    /// Called when dialog is closing. Restores owner/previous dialog state and focus.
    /// </summary>
    protected virtual void OnDialogClosing(Window dialog, Window owner, DialogFocusState focusState)
    {
        Dispatcher.UIThread.Post(() =>
        {
            owner.IsEnabled = true;
            focusState.PreviousFocus?.Focus();
        }, DispatcherPriority.Background);
    }

    /// <summary>
    /// Captures dialog focus state for restoration after closure.
    /// </summary>
    protected sealed class DialogFocusState
    {
        public Window? PreviousDialog { get; }
        public IInputElement? PreviousFocus { get; }

        public DialogFocusState(Window? previousDialog, IInputElement? previousFocus)
        {
            PreviousDialog = previousDialog;
            PreviousFocus = previousFocus;
        }
    }
}
