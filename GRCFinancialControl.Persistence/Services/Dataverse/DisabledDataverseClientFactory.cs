using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerPlatform.Dataverse.Client;

namespace GRCFinancialControl.Persistence.Services.Dataverse;

/// <summary>
/// Represents a disabled <see cref="IDataverseClientFactory" /> used when Dataverse connectivity is not available.
/// </summary>
public sealed class DisabledDataverseClientFactory : IDataverseClientFactory
{
    private const string DisabledMessage = "Dataverse connections are unavailable because the Dataverse backend is not configured.";

    /// <inheritdoc />
    public Task<ServiceClient> CreateAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<ServiceClient>(cancellationToken);
        }

        return Task.FromException<ServiceClient>(new InvalidOperationException(DisabledMessage));
    }
}
