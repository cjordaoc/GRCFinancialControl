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
    private readonly IDataverseClientFactory _clientFactory;

    public DataverseRepository(IDataverseClientFactory clientFactory)
    {
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
    }

    public async Task<TResult> ExecuteAsync<TResult>(Func<ServiceClient, Task<TResult>> operation, CancellationToken cancellationToken = default)
    {
        if (operation is null)
        {
            throw new ArgumentNullException(nameof(operation));
        }

        using var client = await _clientFactory.CreateAsync(cancellationToken);
        return await operation(client);
    }

    public async Task ExecuteAsync(Func<ServiceClient, Task> operation, CancellationToken cancellationToken = default)
    {
        if (operation is null)
        {
            throw new ArgumentNullException(nameof(operation));
        }

        using var client = await _clientFactory.CreateAsync(cancellationToken);
        await operation(client);
    }
}
