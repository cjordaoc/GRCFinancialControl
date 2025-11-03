using CommunityToolkit.Mvvm.Messaging;
using GRC.Shared.UI.Messages;
using GRC.Shared.UI.ViewModels;

namespace GRCFinancialControl.Avalonia.ViewModels;

/// <summary>
/// Serves as the base type for financial control view models that participate in refresh messaging.
/// </summary>
public abstract class ViewModelBase : ValidatableViewModelBase
{
    private static readonly string[] DefaultRefreshTargets = { RefreshTargets.FinancialData };

    /// <summary>
    /// Initializes a new instance of the <see cref="ViewModelBase"/> class.
    /// </summary>
    protected ViewModelBase()
        : this(WeakReferenceMessenger.Default)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ViewModelBase"/> class.
    /// </summary>
    /// <param name="messenger">The messenger used to register for refresh notifications.</param>
    protected ViewModelBase(IMessenger messenger)
        : base(messenger, DefaultRefreshTargets)
    {
    }
}
