using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;

namespace GRC.Shared.UI.Dialogs;

public sealed class ModalDialogSession : IDisposable
{
    private readonly IReadOnlyList<Action> _cleanupActions;

    internal ModalDialogSession(Window dialog, Action focusFirstElement, EventHandler<KeyEventArgs> keyDownHandler, IReadOnlyList<Action> cleanupActions)
    {
        Dialog = dialog;
        FocusFirstElement = focusFirstElement;
        KeyDownHandler = keyDownHandler;
        _cleanupActions = cleanupActions;
    }

    public Window Dialog { get; }

    public Action FocusFirstElement { get; }

    public EventHandler<KeyEventArgs> KeyDownHandler { get; }

    public void Dispose()
    {
        for (var index = _cleanupActions.Count - 1; index >= 0; index--)
        {
            _cleanupActions[index]();
        }
    }
}
