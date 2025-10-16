using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerPlatform.Dataverse.Client;

namespace GRCFinancialControl.Persistence.Services.Dataverse;

/// <summary>
/// Default implementation of <see cref="IDataverseRepository"/> that scopes <see cref="ServiceClient"/> usage.
/// </summary>
public sealed class DataverseRepository : IDataverseRepository
{
    private readonly IDataverseServiceClientFactory _clientFactory;

    public DataverseRepository(IDataverseServiceClientFactory clientFactory)
    {
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
    }

    public async Task<TResult> ExecuteAsync<TResult>(Func<ServiceClient, Task<TResult>> operation, CancellationToken cancellationToken = default)
    {
        if (operation is null)
        {
            throw new ArgumentNullException(nameof(operation));
        }

        using var client = await _clientFactory.CreateClientAsync(cancellationToken).ConfigureAwait(false);
        return await operation(client).ConfigureAwait(false);
    }

    public async Task ExecuteAsync(Func<ServiceClient, Task> operation, CancellationToken cancellationToken = default)
    {
        if (operation is null)
        {
            throw new ArgumentNullException(nameof(operation));
        }

        using var client = await _clientFactory.CreateClientAsync(cancellationToken).ConfigureAwait(false);
        await operation(client).ConfigureAwait(false);
    }
}
