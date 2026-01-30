using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace GRC.Shared.UI.ViewModels;

/// <summary>
/// Provides common messenger plumbing and command helpers for observable view models.
/// </summary>
public abstract class ObservableViewModelBase : ObservableObject
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ObservableViewModelBase"/> class.
    /// </summary>
    /// <param name="messenger">Optional messenger instance used for cross-component communication.</param>
    protected ObservableViewModelBase(IMessenger? messenger = null)
    {
        Messenger = messenger ?? WeakReferenceMessenger.Default;
    }

    /// <summary>
    /// Gets the messenger used to communicate across view models.
    /// </summary>
    protected IMessenger Messenger { get; }

    /// <summary>
    /// Loads any external data required by the view model. Override to supply custom loading logic.
    /// </summary>
    /// <returns>A task that completes when the load cycle ends.</returns>
    public virtual Task LoadDataAsync() => Task.CompletedTask;

    /// <summary>
    /// Notifies the provided command that its execution eligibility may have changed.
    /// </summary>
    /// <param name="command">The command requiring a refresh.</param>
    protected static void NotifyCommandCanExecute(IRelayCommand? command)
    {
        if (command is null)
        {
            return;
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            command.NotifyCanExecuteChanged();
            return;
        }

        Dispatcher.UIThread.Post(command.NotifyCanExecuteChanged);
    }
}
