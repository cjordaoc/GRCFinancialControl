using System;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace GRC.Shared.UI.Messages;

/// <summary>
/// Signals that a view should refresh its data or layout.
/// </summary>
public sealed class RefreshViewMessage : ValueChangedMessage<string?>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RefreshViewMessage"/> class.
    /// </summary>
    /// <param name="target">Optional identifier describing which view should refresh. <c>null</c> means broadcast to all listeners.</param>
    public RefreshViewMessage(string? target = null)
        : base(target)
    {
    }

    /// <summary>
    /// Gets a value indicating whether the refresh request is for all listeners.
    /// </summary>
    public bool IsGlobal => string.IsNullOrWhiteSpace(Value);

    /// <summary>
    /// Determines whether the refresh request applies to the provided target.
    /// </summary>
    /// <param name="target">The target identifier to evaluate.</param>
    /// <returns><c>true</c> when the request should be processed; otherwise, <c>false</c>.</returns>
    public bool Matches(string target)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);

        return IsGlobal || string.Equals(Value, target, StringComparison.Ordinal);
    }
}
