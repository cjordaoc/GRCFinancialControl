using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace GRCFinancialControl.Persistence.Services.Dataverse;

/// <summary>
/// Dataverse-backed implementation of <see cref="ICustomerService"/>.
/// </summary>
public sealed class DataverseCustomerService : DataverseServiceBase, ICustomerService
{
    private readonly DataverseEntityMetadata _metadata;
    private readonly DataverseEntityMetadata _engagementsMetadata;
    private readonly DataverseEntityMetadata _actualsEntriesMetadata;
    private readonly DataverseEntityMetadata _plannedAllocationsMetadata;
    private readonly DataverseEntityMetadata _engagementPapdsMetadata;
    private readonly DataverseEntityMetadata _engagementRankBudgetsMetadata;
    private readonly DataverseEntityMetadata _financialEvolutionsMetadata;
    private readonly DataverseEntityMetadata _fiscalYearAllocationsMetadata;
    private readonly DataverseEntityMetadata _fiscalYearRevenueAllocationsMetadata;

    public DataverseCustomerService(
        IDataverseServiceClientFactory clientFactory,
        DataverseEntityMetadataRegistry metadataRegistry,
        ILogger<DataverseCustomerService> logger)
        : base(clientFactory, metadataRegistry, logger)
    {
        _metadata = metadataRegistry.Get("Customers");
        _engagementsMetadata = metadataRegistry.Get("Engagements");
        _actualsEntriesMetadata = metadataRegistry.Get("ActualsEntries");
        _plannedAllocationsMetadata = metadataRegistry.Get("PlannedAllocations");
        _engagementPapdsMetadata = metadataRegistry.Get("EngagementPapds");
        _engagementRankBudgetsMetadata = metadataRegistry.Get("EngagementRankBudgets");
        _financialEvolutionsMetadata = metadataRegistry.Get("FinancialEvolutions");
        _fiscalYearAllocationsMetadata = metadataRegistry.Get("EngagementFiscalYearAllocations");
        _fiscalYearRevenueAllocationsMetadata = metadataRegistry.Get("EngagementFiscalYearRevenueAllocations");
    }

    public Task<List<Customer>> GetAllAsync()
    {
        return ExecuteAsync(async client =>
        {
            var query = new QueryExpression(_metadata.LogicalName)
            {
                ColumnSet = new ColumnSet(
                    _metadata.GetAttribute("Id"),
                    _metadata.GetAttribute("Name"),
                    _metadata.GetAttribute("CustomerCode"))
            };

            query.AddOrder(_metadata.GetAttribute("Name"), OrderType.Ascending);

            var result = await client.RetrieveMultipleAsync(query).ConfigureAwait(false);
            return result.Entities.Select(Map).ToList();
        });
    }

    public Task AddAsync(Customer customer)
    {
        ArgumentNullException.ThrowIfNull(customer);

        var entity = new Entity(_metadata.LogicalName)
        {
            [_metadata.GetAttribute("Id")] = customer.Id,
            [_metadata.GetAttribute("Name")] = customer.Name,
            [_metadata.GetAttribute("CustomerCode")] = customer.CustomerCode,
        };

        return ExecuteAsync(client => client.CreateAsync(entity));
    }

    public async Task UpdateAsync(Customer customer)
    {
        ArgumentNullException.ThrowIfNull(customer);

        var recordId = await TryResolveRecordIdAsync(
            _metadata,
            _metadata.GetAttribute("Id"),
            customer.Id).ConfigureAwait(false);

        if (recordId is null)
        {
            throw new InvalidOperationException($"Customer with Id={customer.Id} was not found in Dataverse.");
        }

        var entity = new Entity(_metadata.LogicalName)
        {
            Id = recordId.Value,
            [_metadata.GetAttribute("Name")] = customer.Name,
            [_metadata.GetAttribute("CustomerCode")] = customer.CustomerCode,
        };

        await ExecuteAsync(client => client.UpdateAsync(entity)).ConfigureAwait(false);
    }

    public async Task DeleteAsync(int id)
    {
        var recordId = await TryResolveRecordIdAsync(
            _metadata,
            _metadata.GetAttribute("Id"),
            id).ConfigureAwait(false);

        if (recordId is null)
        {
            return;
        }

        await ExecuteAsync(client => client.DeleteAsync(_metadata.LogicalName, recordId.Value)).ConfigureAwait(false);
    }

    public Task DeleteDataAsync(int customerId)
    {
        if (customerId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(customerId), customerId, "Customer identifier must be positive.");
        }

        return ExecuteAsync(async client =>
        {
            var engagementsQuery = new QueryExpression(_engagementsMetadata.LogicalName)
            {
                ColumnSet = new ColumnSet(
                    _engagementsMetadata.PrimaryIdAttribute,
                    _engagementsMetadata.GetAttribute("Id"))
            };

            engagementsQuery.Criteria.AddCondition(
                _engagementsMetadata.GetAttribute("CustomerId"),
                ConditionOperator.Equal,
                customerId);

            var engagementsResult = await client.RetrieveMultipleAsync(engagementsQuery).ConfigureAwait(false);
            if (engagementsResult.Entities.Count == 0)
            {
                Logger.LogInformation("No engagements found in Dataverse for customer {CustomerId}. Nothing to delete.", customerId);
                return;
            }

            var engagementSqlIds = engagementsResult.Entities
                .Select(entity => entity.GetInt(_engagementsMetadata.GetAttribute("Id")))
                .Where(id => id > 0)
                .Distinct()
                .ToArray();

            if (engagementSqlIds.Length == 0)
            {
                Logger.LogWarning("Engagements linked to customer {CustomerId} do not expose SQL identifiers; skipping cascade delete.", customerId);
                return;
            }

            await DeleteEntitiesByAttributeAsync(client, _actualsEntriesMetadata, "EngagementId", engagementSqlIds).ConfigureAwait(false);
            await DeleteEntitiesByAttributeAsync(client, _plannedAllocationsMetadata, "EngagementId", engagementSqlIds).ConfigureAwait(false);
            await DeleteEntitiesByAttributeAsync(client, _engagementPapdsMetadata, "EngagementId", engagementSqlIds).ConfigureAwait(false);
            await DeleteEntitiesByAttributeAsync(client, _engagementRankBudgetsMetadata, "EngagementId", engagementSqlIds).ConfigureAwait(false);
            await DeleteEntitiesByAttributeAsync(client, _financialEvolutionsMetadata, "EngagementId", engagementSqlIds).ConfigureAwait(false);
            await DeleteEntitiesByAttributeAsync(client, _fiscalYearAllocationsMetadata, "EngagementId", engagementSqlIds).ConfigureAwait(false);
            await DeleteEntitiesByAttributeAsync(client, _fiscalYearRevenueAllocationsMetadata, "EngagementId", engagementSqlIds).ConfigureAwait(false);

            foreach (var engagement in engagementsResult.Entities)
            {
                await client.DeleteAsync(_engagementsMetadata.LogicalName, engagement.Id).ConfigureAwait(false);
            }

            Logger.LogInformation(
                "Deleted {EngagementCount} engagement(s) and related data for customer {CustomerId} in Dataverse.",
                engagementsResult.Entities.Count,
                customerId);
        });
    }

    private Customer Map(Entity entity)
    {
        return new Customer
        {
            Id = entity.GetInt(_metadata.GetAttribute("Id")),
            Name = entity.GetString(_metadata.GetAttribute("Name")),
            CustomerCode = entity.GetString(_metadata.GetAttribute("CustomerCode"))
        };
    }

    private async Task DeleteEntitiesByAttributeAsync(
        ServiceClient client,
        DataverseEntityMetadata metadata,
        string attributeKey,
        IReadOnlyCollection<int> engagementIds)
    {
        if (engagementIds.Count == 0)
        {
            return;
        }

        var attribute = metadata.GetAttribute(attributeKey);

        foreach (var batch in engagementIds.Chunk(100))
        {
            var query = new QueryExpression(metadata.LogicalName)
            {
                ColumnSet = new ColumnSet(metadata.PrimaryIdAttribute),
                PageInfo = new PagingInfo
                {
                    PageNumber = 1,
                    Count = 5000,
                }
            };

            query.Criteria.AddCondition(attribute, ConditionOperator.In, batch.Cast<object>().ToArray());

            EntityCollection result;
            do
            {
                result = await client.RetrieveMultipleAsync(query).ConfigureAwait(false);
                foreach (var entity in result.Entities)
                {
                    await client.DeleteAsync(metadata.LogicalName, entity.Id).ConfigureAwait(false);
                }

                if (result.MoreRecords)
                {
                    query.PageInfo.PageNumber++;
                    query.PageInfo.PagingCookie = result.PagingCookie;
                }
                else
                {
                    break;
                }
            }
            while (true);
        }
    }
}
