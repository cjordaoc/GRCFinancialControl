using System;
using System.Threading;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Authentication;

namespace GRCFinancialControl.Persistence.Authentication;

/// <summary>
/// Represents a disabled implementation of <see cref="IInteractiveAuthService" /> used when Dataverse is not configured.
/// </summary>
public sealed class DisabledInteractiveAuthService : IInteractiveAuthService
{
    private const string DisabledMessage = "Interactive authentication is unavailable because the Dataverse backend is not configured.";

    /// <inheritdoc />
    public Task<AuthResult> AcquireTokenAsync(string[] scopes, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<AuthResult>(cancellationToken);
        }

        return Task.FromException<AuthResult>(new InvalidOperationException(DisabledMessage));
    }

    /// <inheritdoc />
    public Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<UserInfo?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<UserInfo?>(cancellationToken);
        }

        return Task.FromResult<UserInfo?>(null);
    }
}
