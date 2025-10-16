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
public abstract class DataverseServiceBase : IDataverseService
{
    private readonly IDataverseRepository _repository;
    protected readonly DataverseEntityMetadataRegistry MetadataRegistry;
    protected readonly ILogger Logger;

    protected DataverseServiceBase(
        IDataverseRepository repository,
        DataverseEntityMetadataRegistry metadataRegistry,
        ILogger logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        MetadataRegistry = metadataRegistry ?? throw new ArgumentNullException(nameof(metadataRegistry));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected async Task<TResult> ExecuteAsync<TResult>(Func<ServiceClient, Task<TResult>> operation, CancellationToken cancellationToken = default)
    {
        if (operation is null)
        {
            throw new ArgumentNullException(nameof(operation));
        }

        return await _repository.ExecuteAsync(operation, cancellationToken).ConfigureAwait(false);
    }

    protected async Task ExecuteAsync(Func<ServiceClient, Task> operation, CancellationToken cancellationToken = default)
    {
        if (operation is null)
        {
            throw new ArgumentNullException(nameof(operation));
        }

        await _repository.ExecuteAsync(operation, cancellationToken).ConfigureAwait(false);
    }

    protected DataverseEntityMetadata GetMetadata(string logicalName)
    {
        if (string.IsNullOrWhiteSpace(logicalName))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(logicalName));
        }

        return MetadataRegistry.Get(logicalName);
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

    Task<TResult> IDataverseService.ExecuteAsync<TResult>(Func<ServiceClient, Task<TResult>> operation, CancellationToken cancellationToken)
    {
        return ExecuteAsync(operation, cancellationToken);
    }

    Task IDataverseService.ExecuteAsync(Func<ServiceClient, Task> operation, CancellationToken cancellationToken)
    {
        return ExecuteAsync(operation, cancellationToken);
    }

    DataverseEntityMetadata IDataverseService.GetEntityMetadata(string logicalName)
    {
        return GetMetadata(logicalName);
    }
}
