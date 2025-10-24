using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    /// <summary>
    /// Serves as the base type for view models that participate in refresh messaging.
    /// </summary>
    public abstract class ViewModelBase : ObservableObject, IRecipient<RefreshDataMessage>
    {
        /// <summary>
        /// Gets the messenger responsible for delivering application-wide notifications.
        /// </summary>
        protected IMessenger Messenger { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ViewModelBase"/> class with the default messenger.
        /// </summary>
        protected ViewModelBase() : this(WeakReferenceMessenger.Default)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ViewModelBase"/> class.
        /// </summary>
        /// <param name="messenger">The messenger used to register for refresh notifications.</param>
        protected ViewModelBase(IMessenger messenger)
        {
            ArgumentNullException.ThrowIfNull(messenger);

            Messenger = messenger;
            Messenger.RegisterAll(this);
        }

        /// <summary>
        /// Loads any external data required by the view model.
        /// </summary>
        /// <returns>A completed task once the load cycle finishes.</returns>
        public virtual Task LoadDataAsync() => Task.CompletedTask;

        /// <summary>
        /// Reacts to refresh requests by loading data again.
        /// </summary>
        /// <param name="message">The refresh message broadcast by other components.</param>
        public virtual void Receive(RefreshDataMessage message)
        {
            _ = LoadDataAsync();
        }

        /// <summary>
        /// Notifies a command that its CanExecute state may have changed.
        /// </summary>
        /// <param name="command">The command requiring a <c>CanExecute</c> refresh.</param>
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
}
