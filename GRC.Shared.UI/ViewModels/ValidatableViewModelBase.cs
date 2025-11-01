using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRC.Shared.UI.Messages;

namespace GRC.Shared.UI.ViewModels;

/// <summary>
/// Provides messenger-driven refresh handling and command helpers for validatable view models.
/// </summary>
public abstract class ValidatableViewModelBase : ObservableValidator, IRecipient<RefreshViewMessage>
{
    private readonly IReadOnlyCollection<string>? _autoRefreshTargets;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidatableViewModelBase"/> class.
    /// </summary>
    /// <param name="messenger">Optional messenger instance used for cross-component communication.</param>
    /// <param name="autoRefreshTargets">Optional refresh targets that should trigger an automatic reload.</param>
    protected ValidatableViewModelBase(IMessenger? messenger = null, IEnumerable<string>? autoRefreshTargets = null)
    {
        Messenger = messenger ?? WeakReferenceMessenger.Default;
        _autoRefreshTargets = autoRefreshTargets?.Where(static target => !string.IsNullOrWhiteSpace(target)).ToArray();
        Messenger.RegisterAll(this);
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

    /// <inheritdoc />
    public virtual void Receive(RefreshViewMessage message)
    {
        if (_autoRefreshTargets is null)
        {
            return;
        }

        if (_autoRefreshTargets.Any(message.Matches))
        {
            _ = LoadDataAsync();
        }
    }

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
