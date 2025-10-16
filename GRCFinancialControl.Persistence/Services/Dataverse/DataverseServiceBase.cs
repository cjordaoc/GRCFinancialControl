using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk.Query;

namespace GRCFinancialControl.Persistence.Services.Dataverse;

/// <summary>
/// Provides shared helpers for Dataverse-backed services.
/// </summary>
public abstract class DataverseServiceBase
{
    private readonly IDataverseServiceClientFactory _clientFactory;
    protected readonly DataverseEntityMetadataRegistry MetadataRegistry;
    protected readonly ILogger Logger;

    protected DataverseServiceBase(
        IDataverseServiceClientFactory clientFactory,
        DataverseEntityMetadataRegistry metadataRegistry,
        ILogger logger)
    {
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        MetadataRegistry = metadataRegistry ?? throw new ArgumentNullException(nameof(metadataRegistry));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected async Task<TResult> ExecuteAsync<TResult>(Func<ServiceClient, Task<TResult>> operation, CancellationToken cancellationToken = default)
    {
        if (operation is null)
        {
            throw new ArgumentNullException(nameof(operation));
        }

        using var client = await _clientFactory.CreateClientAsync(cancellationToken).ConfigureAwait(false);
        return await operation(client).ConfigureAwait(false);
    }

    protected async Task ExecuteAsync(Func<ServiceClient, Task> operation, CancellationToken cancellationToken = default)
    {
        if (operation is null)
        {
            throw new ArgumentNullException(nameof(operation));
        }

        using var client = await _clientFactory.CreateClientAsync(cancellationToken).ConfigureAwait(false);
        await operation(client).ConfigureAwait(false);
    }

    protected Task<Guid?> TryResolveRecordIdAsync(
        DataverseEntityMetadata metadata,
        string attribute,
        object value,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(async client =>
        {
            var query = new QueryExpression(metadata.LogicalName)
            {
                ColumnSet = new ColumnSet(metadata.PrimaryIdAttribute),
                TopCount = 1,
            };

            query.Criteria.AddCondition(attribute, ConditionOperator.Equal, value);

            var result = await client.RetrieveMultipleAsync(query).ConfigureAwait(false);
            if (result.Entities.Count == 0)
            {
                return (Guid?)null;
            }

            return result.Entities[0].Id;
        }, cancellationToken);
    }
}
