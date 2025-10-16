using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Dataverse;
using GRCFinancialControl.Persistence.Services.Dataverse.Schema;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Invoices.Core.Enums;
using Invoices.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;

namespace Invoices.Data.Repositories;

public class DataverseInvoicePlanRepository : DataverseServiceBase, IInvoicePlanRepository
{
    private static readonly IReadOnlyDictionary<InvoiceItemStatus, InvoiceItemStatus[]> StatusTransitions = new ReadOnlyDictionary<InvoiceItemStatus, InvoiceItemStatus[]>(
        new Dictionary<InvoiceItemStatus, InvoiceItemStatus[]>
        {
            [InvoiceItemStatus.Planned] = new[] { InvoiceItemStatus.Requested },
            [InvoiceItemStatus.Requested] = new[] { InvoiceItemStatus.Planned, InvoiceItemStatus.Closed, InvoiceItemStatus.Canceled },
        });

    private readonly DataverseEntityMetadata _planMetadata;
    private readonly DataverseEntityMetadata _itemMetadata;
    private readonly DataverseEntityMetadata _emailMetadata;
    private readonly DataverseEntityMetadata _engagementMetadata;
    private readonly DataverseEntityMetadata _customerMetadata;
    private readonly ILogger<DataverseInvoicePlanRepository> _logger;
    private readonly IPersonDirectory _personDirectory;

    public DataverseInvoicePlanRepository(
        IDataverseRepository repository,
        DataverseEntityMetadataRegistry metadataRegistry,
        ILogger<DataverseInvoicePlanRepository> logger,
        IPersonDirectory personDirectory)
        : base(repository, metadataRegistry, logger)
    {
        _planMetadata = GetMetadata(DataverseSchemaConstants.InvoicePlans.Key);
        _itemMetadata = GetMetadata(DataverseSchemaConstants.InvoiceItems.Key);
        _emailMetadata = GetMetadata(DataverseSchemaConstants.InvoicePlanEmails.Key);
        _engagementMetadata = GetMetadata(DataverseSchemaConstants.Engagements.Key);
        _customerMetadata = GetMetadata(DataverseSchemaConstants.Customers.Key);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _personDirectory = personDirectory ?? throw new ArgumentNullException(nameof(personDirectory));
    }

    protected virtual Task<EntityCollection> RetrieveAsync(
        ServiceClient client,
        QueryBase query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(query);

        return client.RetrieveMultipleAsync(query, cancellationToken);
    }

    protected virtual Task<OrganizationResponse> ExecuteAsync(
        ServiceClient client,
        OrganizationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(request);

        return client.ExecuteAsync(request, cancellationToken);
    }

    public InvoicePlan? GetPlan(int planId)
    {
        if (planId <= 0)
        {
            return null;
        }

        return ExecuteAsync(async client =>
        {
            var planEntity = await RetrievePlanBySqlIdAsync(client, planId).ConfigureAwait(false);
            if (planEntity is null)
            {
                return (InvoicePlan?)null;
            }

            var itemEntities = await RetrievePlanItemEntitiesAsync(client, planId).ConfigureAwait(false);
            var emailEntities = await RetrievePlanEmailEntitiesAsync(client, planId).ConfigureAwait(false);

            return MapPlan(planEntity, itemEntities, emailEntities);
        }).GetAwaiter().GetResult();
    }

    public IReadOnlyList<InvoicePlan> ListPlansForEngagement(string engagementId)
    {
        if (string.IsNullOrWhiteSpace(engagementId))
        {
            return Array.Empty<InvoicePlan>();
        }

        return ExecuteAsync(async client =>
        {
            var plans = await RetrievePlansForEngagementAsync(client, engagementId.Trim()).ConfigureAwait(false);
            if (plans.Count == 0)
            {
                return (IReadOnlyList<InvoicePlan>)Array.Empty<InvoicePlan>();
            }

            var planIds = plans.Select(plan => plan.GetInt(_planMetadata.GetAttribute("Id"))).ToArray();
            var itemLookup = await RetrieveItemsForPlansAsync(client, planIds).ConfigureAwait(false);
            var emailLookup = await RetrieveEmailsForPlansAsync(client, planIds).ConfigureAwait(false);

            var result = new List<InvoicePlan>(plans.Count);
            foreach (var entity in plans.OrderBy(plan => plan.GetDateTime(_planMetadata.GetAttribute("CreatedAt")) ?? DateTime.UtcNow))
            {
                var sqlId = entity.GetInt(_planMetadata.GetAttribute("Id"));
                itemLookup.TryGetValue(sqlId, out var items);
                emailLookup.TryGetValue(sqlId, out var emails);
                items ??= new List<Entity>();
                emails ??= new List<Entity>();
                result.Add(MapPlan(entity, items, emails));
            }

            return (IReadOnlyList<InvoicePlan>)result;
        }).GetAwaiter().GetResult();
    }

    public RepositorySaveResult SavePlan(InvoicePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return ExecuteAsync(async client => await SavePlanInternalAsync(client, plan).ConfigureAwait(false)).GetAwaiter().GetResult();
    }

    public RepositorySaveResult MarkItemsAsRequested(int planId, IReadOnlyCollection<InvoiceRequestUpdate> updates)
    {
        if (updates is null)
        {
            throw new ArgumentNullException(nameof(updates));
        }

        if (updates.Count == 0)
        {
            return RepositorySaveResult.Empty;
        }

        return ExecuteAsync(async client => await UpdateRequestStatusAsync(client, planId, updates, InvoiceItemStatus.Requested).ConfigureAwait(false)).GetAwaiter().GetResult();
    }

    public RepositorySaveResult UndoRequest(int planId, IReadOnlyCollection<int> itemIds)
    {
        if (itemIds is null)
        {
            throw new ArgumentNullException(nameof(itemIds));
        }

        if (itemIds.Count == 0)
        {
            return RepositorySaveResult.Empty;
        }

        return ExecuteAsync(async client => await UndoRequestInternalAsync(client, planId, itemIds).ConfigureAwait(false)).GetAwaiter().GetResult();
    }

    public RepositorySaveResult CloseItems(int planId, IReadOnlyCollection<InvoiceEmissionUpdate> updates)
    {
        if (updates is null)
        {
            throw new ArgumentNullException(nameof(updates));
        }

        if (updates.Count == 0)
        {
            return RepositorySaveResult.Empty;
        }

        return ExecuteAsync(async client => await CloseItemsInternalAsync(client, planId, updates).ConfigureAwait(false)).GetAwaiter().GetResult();
    }

    public RepositorySaveResult CancelAndReissue(int planId, IReadOnlyCollection<InvoiceReissueRequest> requests)
    {
        if (requests is null)
        {
            throw new ArgumentNullException(nameof(requests));
        }

        if (requests.Count == 0)
        {
            return RepositorySaveResult.Empty;
        }

        return ExecuteAsync(async client => await CancelAndReissueInternalAsync(client, planId, requests).ConfigureAwait(false)).GetAwaiter().GetResult();
    }

    public InvoiceSummaryResult SearchSummary(InvoiceSummaryFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        return ExecuteAsync(async client => await BuildSummaryAsync(client, filter).ConfigureAwait(false)).GetAwaiter().GetResult();
    }

    public IReadOnlyList<InvoiceNotificationPreview> PreviewNotifications(DateTime notificationDate)
    {
        return ExecuteAsync(async client => await PreviewNotificationsInternalAsync(client, notificationDate).ConfigureAwait(false)).GetAwaiter().GetResult();
    }

    private async Task<Entity?> RetrievePlanBySqlIdAsync(ServiceClient client, int planId, CancellationToken cancellationToken = default)
    {
        var query = new QueryExpression(_planMetadata.LogicalName)
        {
            ColumnSet = new ColumnSet(
                _planMetadata.GetAttribute("Id"),
                _planMetadata.GetAttribute("EngagementId"),
                _planMetadata.GetAttribute("Engagement"),
                _planMetadata.GetAttribute("Type"),
                _planMetadata.GetAttribute("NumInvoices"),
                _planMetadata.GetAttribute("PaymentTermDays"),
                _planMetadata.GetAttribute("CustomerFocalPointName"),
                _planMetadata.GetAttribute("CustomerFocalPointEmail"),
                _planMetadata.GetAttribute("CustomInstructions"),
                _planMetadata.GetAttribute("FirstEmissionDate"),
                _planMetadata.GetAttribute("CreatedAt"),
                _planMetadata.GetAttribute("UpdatedAt")),
            TopCount = 1,
        };

        query.Criteria.AddCondition(_planMetadata.GetAttribute("Id"), ConditionOperator.Equal, planId);

        var result = await RetrieveAsync(client, query, cancellationToken).ConfigureAwait(false);
        return result.Entities.Count > 0 ? result.Entities[0] : null;
    }

    private async Task<List<Entity>> RetrievePlansForEngagementAsync(ServiceClient client, string engagementId, CancellationToken cancellationToken = default)
    {
        var query = new QueryExpression(_planMetadata.LogicalName)
        {
            ColumnSet = new ColumnSet(
                _planMetadata.GetAttribute("Id"),
                _planMetadata.GetAttribute("EngagementId"),
                _planMetadata.GetAttribute("Engagement"),
                _planMetadata.GetAttribute("Type"),
                _planMetadata.GetAttribute("NumInvoices"),
                _planMetadata.GetAttribute("PaymentTermDays"),
                _planMetadata.GetAttribute("CustomerFocalPointName"),
                _planMetadata.GetAttribute("CustomerFocalPointEmail"),
                _planMetadata.GetAttribute("CustomInstructions"),
                _planMetadata.GetAttribute("FirstEmissionDate"),
                _planMetadata.GetAttribute("CreatedAt"),
                _planMetadata.GetAttribute("UpdatedAt")),
        };

        query.Criteria.AddCondition(_planMetadata.GetAttribute("EngagementId"), ConditionOperator.Equal, engagementId);

        var result = await RetrieveAsync(client, query, cancellationToken).ConfigureAwait(false);
        return result.Entities.ToList();
    }

    private async Task<IReadOnlyCollection<Entity>> RetrievePlanItemEntitiesAsync(ServiceClient client, int planId, CancellationToken cancellationToken = default)
    {
        var query = new QueryExpression(_itemMetadata.LogicalName)
        {
            ColumnSet = new ColumnSet(
                _itemMetadata.GetAttribute("Id"),
                _itemMetadata.GetAttribute("InvoicePlan"),
                _itemMetadata.GetAttribute("InvoicePlanSqlId"),
                _itemMetadata.GetAttribute("Sequence"),
                _itemMetadata.GetAttribute("Description"),
                _itemMetadata.GetAttribute("Amount"),
                _itemMetadata.GetAttribute("ScheduledDate"),
                _itemMetadata.GetAttribute("DueDate"),
                _itemMetadata.GetAttribute("Percentage"),
                _itemMetadata.GetAttribute("PayerCnpj"),
                _itemMetadata.GetAttribute("PoNumber"),
                _itemMetadata.GetAttribute("FrsNumber"),
                _itemMetadata.GetAttribute("CustomerTicket"),
                _itemMetadata.GetAttribute("AdditionalInfo"),
                _itemMetadata.GetAttribute("Status"),
                _itemMetadata.GetAttribute("RitmNumber"),
                _itemMetadata.GetAttribute("CoeResponsible"),
                _itemMetadata.GetAttribute("RequestDate"),
                _itemMetadata.GetAttribute("BzCode"),
                _itemMetadata.GetAttribute("EmittedAt"),
                _itemMetadata.GetAttribute("CanceledAt"),
                _itemMetadata.GetAttribute("CancelReason"),
                _itemMetadata.GetAttribute("ReplacementItem"),
                _itemMetadata.GetAttribute("ReplacementItemSqlId")),
            Orders =
            {
                new OrderExpression(_itemMetadata.GetAttribute("Sequence"), OrderType.Ascending),
            }
        };

        query.Criteria.AddCondition(_itemMetadata.GetAttribute("InvoicePlanSqlId"), ConditionOperator.Equal, planId);

        var result = await RetrieveAsync(client, query, cancellationToken).ConfigureAwait(false);
        return result.Entities;
    }

    private async Task<IReadOnlyCollection<Entity>> RetrievePlanEmailEntitiesAsync(ServiceClient client, int planId, CancellationToken cancellationToken = default)
    {
        var query = new QueryExpression(_emailMetadata.LogicalName)
        {
            ColumnSet = new ColumnSet(
                _emailMetadata.GetAttribute("Id"),
                _emailMetadata.GetAttribute("InvoicePlan"),
                _emailMetadata.GetAttribute("InvoicePlanSqlId"),
                _emailMetadata.GetAttribute("Email"),
                _emailMetadata.GetAttribute("CreatedAt"),
                _emailMetadata.GetAttribute("UpdatedAt"))
        };

        query.Criteria.AddCondition(_emailMetadata.GetAttribute("InvoicePlanSqlId"), ConditionOperator.Equal, planId);

        var result = await RetrieveAsync(client, query, cancellationToken).ConfigureAwait(false);
        return result.Entities;
    }

    private async Task<Dictionary<int, List<Entity>>> RetrieveItemsForPlansAsync(ServiceClient client, IReadOnlyCollection<int> planIds, CancellationToken cancellationToken = default)
    {
        if (planIds.Count == 0)
        {
            return new Dictionary<int, List<Entity>>();
        }

        var query = new QueryExpression(_itemMetadata.LogicalName)
        {
            ColumnSet = new ColumnSet(
                _itemMetadata.GetAttribute("Id"),
                _itemMetadata.GetAttribute("InvoicePlanSqlId"),
                _itemMetadata.GetAttribute("Sequence"),
                _itemMetadata.GetAttribute("Description"),
                _itemMetadata.GetAttribute("Amount"),
                _itemMetadata.GetAttribute("ScheduledDate"),
                _itemMetadata.GetAttribute("DueDate"),
                _itemMetadata.GetAttribute("Percentage"),
                _itemMetadata.GetAttribute("PayerCnpj"),
                _itemMetadata.GetAttribute("PoNumber"),
                _itemMetadata.GetAttribute("FrsNumber"),
                _itemMetadata.GetAttribute("CustomerTicket"),
                _itemMetadata.GetAttribute("AdditionalInfo"),
                _itemMetadata.GetAttribute("Status"),
                _itemMetadata.GetAttribute("RitmNumber"),
                _itemMetadata.GetAttribute("CoeResponsible"),
                _itemMetadata.GetAttribute("RequestDate"),
                _itemMetadata.GetAttribute("BzCode"),
                _itemMetadata.GetAttribute("EmittedAt"),
                _itemMetadata.GetAttribute("CanceledAt"),
                _itemMetadata.GetAttribute("CancelReason"),
                _itemMetadata.GetAttribute("ReplacementItem"),
                _itemMetadata.GetAttribute("ReplacementItemSqlId"))
        };

        query.Criteria.AddCondition(_itemMetadata.GetAttribute("InvoicePlanSqlId"), ConditionOperator.In, planIds.Cast<object>().ToArray());

        var result = await RetrieveAsync(client, query, cancellationToken).ConfigureAwait(false);

        var lookup = new Dictionary<int, List<Entity>>();
        foreach (var entity in result.Entities)
        {
            var planSqlId = entity.GetInt(_itemMetadata.GetAttribute("InvoicePlanSqlId"));
            if (!lookup.TryGetValue(planSqlId, out var list))
            {
                list = new List<Entity>();
                lookup[planSqlId] = list;
            }

            list.Add(entity);
        }

        foreach (var list in lookup.Values)
        {
            list.Sort((left, right) => left.GetInt(_itemMetadata.GetAttribute("Sequence")).CompareTo(right.GetInt(_itemMetadata.GetAttribute("Sequence"))));
        }

        return lookup;
    }

    private async Task<Dictionary<int, List<Entity>>> RetrieveEmailsForPlansAsync(ServiceClient client, IReadOnlyCollection<int> planIds, CancellationToken cancellationToken = default)
    {
        if (planIds.Count == 0)
        {
            return new Dictionary<int, List<Entity>>();
        }

        var query = new QueryExpression(_emailMetadata.LogicalName)
        {
            ColumnSet = new ColumnSet(
                _emailMetadata.GetAttribute("Id"),
                _emailMetadata.GetAttribute("InvoicePlanSqlId"),
                _emailMetadata.GetAttribute("Email"),
                _emailMetadata.GetAttribute("CreatedAt"),
                _emailMetadata.GetAttribute("UpdatedAt"))
        };

        query.Criteria.AddCondition(_emailMetadata.GetAttribute("InvoicePlanSqlId"), ConditionOperator.In, planIds.Cast<object>().ToArray());

        var result = await RetrieveAsync(client, query, cancellationToken).ConfigureAwait(false);
        var lookup = new Dictionary<int, List<Entity>>();
        foreach (var entity in result.Entities)
        {
            var planSqlId = entity.GetInt(_emailMetadata.GetAttribute("InvoicePlanSqlId"));
            if (!lookup.TryGetValue(planSqlId, out var list))
            {
                list = new List<Entity>();
                lookup[planSqlId] = list;
            }

            list.Add(entity);
        }

        return lookup;
    }

    private InvoicePlan MapPlan(Entity planEntity, IReadOnlyCollection<Entity> itemEntities, IReadOnlyCollection<Entity> emailEntities)
    {
        var plan = new InvoicePlan
        {
            Id = planEntity.GetInt(_planMetadata.GetAttribute("Id")),
            EngagementId = planEntity.GetString(_planMetadata.GetAttribute("EngagementId")),
            Type = (InvoicePlanType)planEntity.GetInt(_planMetadata.GetAttribute("Type")),
            NumInvoices = planEntity.GetInt(_planMetadata.GetAttribute("NumInvoices")),
            PaymentTermDays = planEntity.GetInt(_planMetadata.GetAttribute("PaymentTermDays")),
            CustomerFocalPointName = planEntity.GetString(_planMetadata.GetAttribute("CustomerFocalPointName")),
            CustomerFocalPointEmail = planEntity.GetString(_planMetadata.GetAttribute("CustomerFocalPointEmail")),
            CustomInstructions = planEntity.GetOptionalString(_planMetadata.GetAttribute("CustomInstructions")),
            FirstEmissionDate = planEntity.GetDateTime(_planMetadata.GetAttribute("FirstEmissionDate"))?.Date,
            CreatedAt = planEntity.GetDateTime(_planMetadata.GetAttribute("CreatedAt")) ?? DateTime.UtcNow,
            UpdatedAt = planEntity.GetDateTime(_planMetadata.GetAttribute("UpdatedAt")) ?? DateTime.UtcNow,
        };

        var items = new List<InvoiceItem>(itemEntities.Count);
        foreach (var entity in itemEntities)
        {
            var item = MapItem(entity, plan.Id);
            items.Add(item);
        }

        var itemsBySqlId = items.ToDictionary(item => item.Id);
        foreach (var item in items)
        {
            if (item.ReplacementItemId.HasValue && itemsBySqlId.TryGetValue(item.ReplacementItemId.Value, out var replacement))
            {
                item.ReplacementItem = replacement;
            }
        }

        plan.Items.Clear();
        foreach (var item in items.OrderBy(item => item.SeqNo))
        {
            plan.Items.Add(item);
        }

        foreach (var entity in emailEntities)
        {
            plan.AdditionalEmails.Add(MapEmail(entity, plan.Id));
        }

        return plan;
    }

    private InvoiceItem MapItem(Entity entity, int planId)
    {
        var item = new InvoiceItem
        {
            Id = entity.GetInt(_itemMetadata.GetAttribute("Id")),
            PlanId = planId,
            SeqNo = entity.GetInt(_itemMetadata.GetAttribute("Sequence")),
            DeliveryDescription = entity.GetOptionalString(_itemMetadata.GetAttribute("Description")),
            Amount = entity.GetDecimal(_itemMetadata.GetAttribute("Amount")),
            Percentage = entity.GetDecimal(_itemMetadata.GetAttribute("Percentage")),
            EmissionDate = entity.GetDateTime(_itemMetadata.GetAttribute("ScheduledDate"))?.Date,
            DueDate = entity.GetDateTime(_itemMetadata.GetAttribute("DueDate"))?.Date,
            PayerCnpj = entity.GetString(_itemMetadata.GetAttribute("PayerCnpj")),
            PoNumber = entity.GetOptionalString(_itemMetadata.GetAttribute("PoNumber")),
            FrsNumber = entity.GetOptionalString(_itemMetadata.GetAttribute("FrsNumber")),
            CustomerTicket = entity.GetOptionalString(_itemMetadata.GetAttribute("CustomerTicket")),
            AdditionalInfo = entity.GetOptionalString(_itemMetadata.GetAttribute("AdditionalInfo")),
            Status = (InvoiceItemStatus)entity.GetInt(_itemMetadata.GetAttribute("Status")),
            RitmNumber = entity.GetOptionalString(_itemMetadata.GetAttribute("RitmNumber")),
            CoeResponsible = entity.GetOptionalString(_itemMetadata.GetAttribute("CoeResponsible")),
            RequestDate = entity.GetDateTime(_itemMetadata.GetAttribute("RequestDate"))?.Date,
            BzCode = entity.GetOptionalString(_itemMetadata.GetAttribute("BzCode")),
            EmittedAt = entity.GetDateTime(_itemMetadata.GetAttribute("EmittedAt"))?.Date,
            CanceledAt = entity.GetDateTime(_itemMetadata.GetAttribute("CanceledAt"))?.Date,
            CancelReason = entity.GetOptionalString(_itemMetadata.GetAttribute("CancelReason")),
            ReplacementItemId = entity.GetNullableInt(_itemMetadata.GetAttribute("ReplacementItemSqlId")),
        };

        return item;
    }

    private InvoicePlanEmail MapEmail(Entity entity, int planId)
    {
        return new InvoicePlanEmail
        {
            Id = entity.GetInt(_emailMetadata.GetAttribute("Id")),
            PlanId = planId,
            Email = entity.GetString(_emailMetadata.GetAttribute("Email")),
            CreatedAt = entity.GetDateTime(_emailMetadata.GetAttribute("CreatedAt")) ?? DateTime.UtcNow,
        };
    }

    private async Task<RepositorySaveResult> SavePlanInternalAsync(ServiceClient client, InvoicePlan plan, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var planEntity = await RetrievePlanBySqlIdAsync(client, plan.Id, cancellationToken).ConfigureAwait(false);
        var planExists = planEntity is not null;

        if (!planExists)
        {
            plan.Id = plan.Id > 0 ? plan.Id : await GetNextSqlIdAsync(client, _planMetadata, cancellationToken).ConfigureAwait(false);
            plan.CreatedAt = now;
        }
        else
        {
            plan.CreatedAt = planEntity!.GetDateTime(_planMetadata.GetAttribute("CreatedAt")) ?? now;
        }

        plan.UpdatedAt = now;

        var upsertRequests = new ExecuteMultipleRequest
        {
            Settings = new ExecuteMultipleSettings
            {
                ContinueOnError = false,
                ReturnResponses = false,
            },
            Requests = new OrganizationRequestCollection(),
        };

        var planReference = new EntityReference(_planMetadata.LogicalName);
        planReference.KeyAttributes[_planMetadata.GetAttribute("Id")] = plan.Id;

        var planUpsert = new Entity(_planMetadata.LogicalName)
        {
            [_planMetadata.GetAttribute("EngagementId")] = plan.EngagementId,
            [_planMetadata.GetAttribute("Type")] = new OptionSetValue((int)plan.Type),
            [_planMetadata.GetAttribute("NumInvoices")] = plan.NumInvoices,
            [_planMetadata.GetAttribute("PaymentTermDays")] = plan.PaymentTermDays,
            [_planMetadata.GetAttribute("CustomerFocalPointName")] = plan.CustomerFocalPointName,
            [_planMetadata.GetAttribute("CustomerFocalPointEmail")] = plan.CustomerFocalPointEmail,
            [_planMetadata.GetAttribute("CustomInstructions")] = string.IsNullOrWhiteSpace(plan.CustomInstructions) ? null : plan.CustomInstructions,
            [_planMetadata.GetAttribute("FirstEmissionDate")] = plan.FirstEmissionDate,
            [_planMetadata.GetAttribute("CreatedAt")] = plan.CreatedAt,
            [_planMetadata.GetAttribute("UpdatedAt")] = plan.UpdatedAt,
        };

        planUpsert.KeyAttributes[_planMetadata.GetAttribute("Id")] = plan.Id;

        upsertRequests.Requests.Add(new UpsertRequest { Target = planUpsert });

        var existingItems = await RetrievePlanItemEntitiesAsync(client, plan.Id, cancellationToken).ConfigureAwait(false);
        var existingItemLookup = existingItems.ToDictionary(item => item.GetInt(_itemMetadata.GetAttribute("Id")));
        var nextItemSqlId = await GetNextSqlIdAsync(client, _itemMetadata, cancellationToken).ConfigureAwait(false);

        var newItemIds = new HashSet<int>();
        foreach (var item in plan.Items.OrderBy(item => item.SeqNo))
        {
            if (item.Id <= 0)
            {
                item.Id = nextItemSqlId++;
                item.CreatedAt = now;
                item.Status = InvoiceItemStatus.Planned;
                newItemIds.Add(item.Id);
            }

            item.PlanId = plan.Id;
            item.UpdatedAt = now;

            var entity = new Entity(_itemMetadata.LogicalName)
            {
                [_itemMetadata.GetAttribute("InvoicePlanSqlId")] = plan.Id,
                [_itemMetadata.GetAttribute("InvoicePlan")] = planReference,
                [_itemMetadata.GetAttribute("Sequence")] = item.SeqNo,
                [_itemMetadata.GetAttribute("Description")] = string.IsNullOrWhiteSpace(item.DeliveryDescription) ? null : item.DeliveryDescription,
                [_itemMetadata.GetAttribute("Amount")] = item.Amount,
                [_itemMetadata.GetAttribute("Percentage")] = item.Percentage,
                [_itemMetadata.GetAttribute("ScheduledDate")] = item.EmissionDate,
                [_itemMetadata.GetAttribute("DueDate")] = item.DueDate,
                [_itemMetadata.GetAttribute("PayerCnpj")] = item.PayerCnpj,
                [_itemMetadata.GetAttribute("PoNumber")] = item.PoNumber,
                [_itemMetadata.GetAttribute("FrsNumber")] = item.FrsNumber,
                [_itemMetadata.GetAttribute("CustomerTicket")] = item.CustomerTicket,
                [_itemMetadata.GetAttribute("AdditionalInfo")] = item.AdditionalInfo,
                [_itemMetadata.GetAttribute("Status")] = new OptionSetValue((int)item.Status),
                [_itemMetadata.GetAttribute("RitmNumber")] = item.RitmNumber,
                [_itemMetadata.GetAttribute("CoeResponsible")] = item.CoeResponsible,
                [_itemMetadata.GetAttribute("RequestDate")] = item.RequestDate,
                [_itemMetadata.GetAttribute("BzCode")] = item.BzCode,
                [_itemMetadata.GetAttribute("EmittedAt")] = item.EmittedAt,
                [_itemMetadata.GetAttribute("CanceledAt")] = item.CanceledAt,
                [_itemMetadata.GetAttribute("CancelReason")] = item.CancelReason,
                [_itemMetadata.GetAttribute("ReplacementItemSqlId")] = item.ReplacementItemId,
            };

            entity.KeyAttributes[_itemMetadata.GetAttribute("Id")] = item.Id;
            upsertRequests.Requests.Add(new UpsertRequest { Target = entity });
        }

        var removedItems = existingItemLookup.Keys.Except(plan.Items.Select(item => item.Id)).ToArray();
        foreach (var removed in removedItems)
        {
            var entity = existingItemLookup[removed];
            upsertRequests.Requests.Add(new DeleteRequest
            {
                Target = new EntityReference(_itemMetadata.LogicalName, entity.Id)
            });
        }

        var existingEmails = await RetrievePlanEmailEntitiesAsync(client, plan.Id, cancellationToken).ConfigureAwait(false);
        var existingEmailLookup = existingEmails.ToDictionary(email => email.GetInt(_emailMetadata.GetAttribute("Id")));
        var nextEmailSqlId = await GetNextSqlIdAsync(client, _emailMetadata, cancellationToken).ConfigureAwait(false);

        var currentEmailIds = new HashSet<int>();
        foreach (var email in plan.AdditionalEmails)
        {
            if (email.Id <= 0)
            {
                email.Id = nextEmailSqlId++;
                email.CreatedAt = now;
            }

            email.PlanId = plan.Id;
            currentEmailIds.Add(email.Id);

            var entity = new Entity(_emailMetadata.LogicalName)
            {
                [_emailMetadata.GetAttribute("InvoicePlanSqlId")] = plan.Id,
                [_emailMetadata.GetAttribute("InvoicePlan")] = planReference,
                [_emailMetadata.GetAttribute("Email")] = email.Email,
                [_emailMetadata.GetAttribute("CreatedAt")] = email.CreatedAt,
                [_emailMetadata.GetAttribute("UpdatedAt")] = now,
            };

            entity.KeyAttributes[_emailMetadata.GetAttribute("Id")] = email.Id;
            upsertRequests.Requests.Add(new UpsertRequest { Target = entity });
        }

        var removedEmails = existingEmailLookup.Keys.Except(currentEmailIds).ToArray();
        foreach (var removed in removedEmails)
        {
            var entity = existingEmailLookup[removed];
            upsertRequests.Requests.Add(new DeleteRequest
            {
                Target = new EntityReference(_emailMetadata.LogicalName, entity.Id)
            });
        }

        if (upsertRequests.Requests.Count > 0)
        {
        await ExecuteAsync(client, upsertRequests, cancellationToken).ConfigureAwait(false);
        }

        var createdCount = planExists ? 0 : 1;
        createdCount += newItemIds.Count;
        createdCount += plan.AdditionalEmails.Count(email => !existingEmailLookup.ContainsKey(email.Id));

        var updatedCount = planExists ? 1 : 0;
        updatedCount += plan.Items.Count(item => !newItemIds.Contains(item.Id));
        updatedCount += plan.AdditionalEmails.Count(email => existingEmailLookup.ContainsKey(email.Id));

        var deletedCount = removedItems.Length + removedEmails.Length;
        var affected = createdCount + updatedCount + deletedCount;
        return new RepositorySaveResult(createdCount, updatedCount, deletedCount, affected);
    }

    private async Task<RepositorySaveResult> UpdateRequestStatusAsync(ServiceClient client, int planId, IReadOnlyCollection<InvoiceRequestUpdate> updates, InvoiceItemStatus targetStatus, CancellationToken cancellationToken = default)
    {
        if (planId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(planId));
        }

        var items = await RetrievePlanItemEntitiesAsync(client, planId, cancellationToken).ConfigureAwait(false);
        if (items.Count == 0)
        {
            throw new InvalidOperationException($"Invoice plan {planId} not found.");
        }

        var lookup = items.ToDictionary(item => item.GetInt(_itemMetadata.GetAttribute("Id")));
        var updated = 0;
        var requests = new ExecuteMultipleRequest
        {
            Settings = new ExecuteMultipleSettings
            {
                ContinueOnError = false,
                ReturnResponses = false,
            },
            Requests = new OrganizationRequestCollection(),
        };

        foreach (var update in updates.GroupBy(update => update.ItemId).Select(group => group.Last()))
        {
            if (!lookup.TryGetValue(update.ItemId, out var entity))
            {
                throw new InvalidOperationException($"Invoice item {update.ItemId} not found in plan {planId}.");
            }

            var currentStatus = (InvoiceItemStatus)entity.GetInt(_itemMetadata.GetAttribute("Status"));
            if (!StatusTransitions.TryGetValue(currentStatus, out var allowed) || !allowed.Contains(targetStatus))
            {
                throw new InvalidOperationException($"Invoice item {update.ItemId} is not in a valid state to update.");
            }

            if (targetStatus == InvoiceItemStatus.Requested)
            {
                if (string.IsNullOrWhiteSpace(update.RitmNumber))
                {
                    throw new InvalidOperationException($"RITM number is required for item {update.ItemId}.");
                }

                if (string.IsNullOrWhiteSpace(update.CoeResponsible))
                {
                    throw new InvalidOperationException($"COE responsible is required for item {update.ItemId}.");
                }

                if (update.RequestDate == default)
                {
                    throw new InvalidOperationException($"Request date is required for item {update.ItemId}.");
                }
            }

            var updateEntity = new Entity(_itemMetadata.LogicalName)
            {
                [_itemMetadata.GetAttribute("Status")] = new OptionSetValue((int)targetStatus),
                [_itemMetadata.GetAttribute("RitmNumber")] = string.IsNullOrWhiteSpace(update.RitmNumber) ? null : update.RitmNumber.Trim(),
                [_itemMetadata.GetAttribute("CoeResponsible")] = string.IsNullOrWhiteSpace(update.CoeResponsible) ? null : update.CoeResponsible.Trim(),
                [_itemMetadata.GetAttribute("RequestDate")] = targetStatus == InvoiceItemStatus.Requested ? update.RequestDate.Date : null,
                [_itemMetadata.GetAttribute("BzCode")] = null,
                [_itemMetadata.GetAttribute("EmittedAt")] = null,
            };

            updateEntity.KeyAttributes[_itemMetadata.GetAttribute("Id")] = update.ItemId;
            requests.Requests.Add(new UpdateRequest { Target = updateEntity });
            updated++;
        }

        if (requests.Requests.Count > 0)
        {
        await ExecuteAsync(client, requests, cancellationToken).ConfigureAwait(false);
        }

        return new RepositorySaveResult(0, updated, 0, updated);
    }

    private async Task<RepositorySaveResult> UndoRequestInternalAsync(ServiceClient client, int planId, IReadOnlyCollection<int> itemIds, CancellationToken cancellationToken = default)
    {
        var items = await RetrievePlanItemEntitiesAsync(client, planId, cancellationToken).ConfigureAwait(false);
        if (items.Count == 0)
        {
            throw new InvalidOperationException($"Invoice plan {planId} not found.");
        }

        var lookup = items.ToDictionary(item => item.GetInt(_itemMetadata.GetAttribute("Id")));
        var requests = new ExecuteMultipleRequest
        {
            Settings = new ExecuteMultipleSettings
            {
                ContinueOnError = false,
                ReturnResponses = false,
            },
            Requests = new OrganizationRequestCollection(),
        };

        var updated = 0;
        foreach (var itemId in itemIds.Distinct())
        {
            if (!lookup.TryGetValue(itemId, out var entity))
            {
                throw new InvalidOperationException($"Invoice item {itemId} not found in plan {planId}.");
            }

            var status = (InvoiceItemStatus)entity.GetInt(_itemMetadata.GetAttribute("Status"));
            if (status != InvoiceItemStatus.Requested)
            {
                continue;
            }

            var updateEntity = new Entity(_itemMetadata.LogicalName)
            {
                [_itemMetadata.GetAttribute("Status")] = new OptionSetValue((int)InvoiceItemStatus.Planned),
                [_itemMetadata.GetAttribute("RitmNumber")] = null,
                [_itemMetadata.GetAttribute("CoeResponsible")] = null,
                [_itemMetadata.GetAttribute("RequestDate")] = null,
            };

            updateEntity.KeyAttributes[_itemMetadata.GetAttribute("Id")] = itemId;
            requests.Requests.Add(new UpdateRequest { Target = updateEntity });
            updated++;
        }

        if (requests.Requests.Count > 0)
        {
        await ExecuteAsync(client, requests, cancellationToken).ConfigureAwait(false);
        }

        return new RepositorySaveResult(0, updated, 0, updated);
    }

    private async Task<RepositorySaveResult> CloseItemsInternalAsync(ServiceClient client, int planId, IReadOnlyCollection<InvoiceEmissionUpdate> updates, CancellationToken cancellationToken = default)
    {
        var items = await RetrievePlanItemEntitiesAsync(client, planId, cancellationToken).ConfigureAwait(false);
        if (items.Count == 0)
        {
            throw new InvalidOperationException($"Invoice plan {planId} not found.");
        }

        var lookup = items.ToDictionary(item => item.GetInt(_itemMetadata.GetAttribute("Id")));
        var requests = new ExecuteMultipleRequest
        {
            Settings = new ExecuteMultipleSettings
            {
                ContinueOnError = false,
                ReturnResponses = false,
            },
            Requests = new OrganizationRequestCollection(),
        };

        var updated = 0;
        foreach (var update in updates.GroupBy(update => update.ItemId).Select(group => group.Last()))
        {
            if (!lookup.TryGetValue(update.ItemId, out var entity))
            {
                throw new InvalidOperationException($"Invoice item {update.ItemId} not found in plan {planId}.");
            }

            var status = (InvoiceItemStatus)entity.GetInt(_itemMetadata.GetAttribute("Status"));
            if (status != InvoiceItemStatus.Requested)
            {
                throw new InvalidOperationException($"Invoice item {update.ItemId} is not in Requested status.");
            }

            if (string.IsNullOrWhiteSpace(entity.GetString(_itemMetadata.GetAttribute("RitmNumber"))))
            {
                throw new InvalidOperationException($"Invoice item {update.ItemId} cannot be closed without a RITM.");
            }

            if (string.IsNullOrWhiteSpace(update.BzCode))
            {
                throw new InvalidOperationException($"BZ code is required to close invoice item {update.ItemId}.");
            }

            var emittedDate = update.EmittedAt?.Date ?? DateTime.UtcNow.Date;

            var updateEntity = new Entity(_itemMetadata.LogicalName)
            {
                [_itemMetadata.GetAttribute("Status")] = new OptionSetValue((int)InvoiceItemStatus.Closed),
                [_itemMetadata.GetAttribute("BzCode")] = update.BzCode.Trim(),
                [_itemMetadata.GetAttribute("EmittedAt")] = emittedDate,
            };

            updateEntity.KeyAttributes[_itemMetadata.GetAttribute("Id")] = update.ItemId;
            requests.Requests.Add(new UpdateRequest { Target = updateEntity });
            updated++;
        }

        if (requests.Requests.Count > 0)
        {
        await ExecuteAsync(client, requests, cancellationToken).ConfigureAwait(false);
        }

        return new RepositorySaveResult(0, updated, 0, updated);
    }

    private async Task<RepositorySaveResult> CancelAndReissueInternalAsync(ServiceClient client, int planId, IReadOnlyCollection<InvoiceReissueRequest> requests, CancellationToken cancellationToken = default)
    {
        var planEntity = await RetrievePlanBySqlIdAsync(client, planId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Invoice plan {planId} not found.");

        var items = await RetrievePlanItemEntitiesAsync(client, planId, cancellationToken).ConfigureAwait(false);
        var lookup = items.ToDictionary(item => item.GetInt(_itemMetadata.GetAttribute("Id")));
        var nextItemSqlId = await GetNextSqlIdAsync(client, _itemMetadata, cancellationToken).ConfigureAwait(false);
        var nextSequence = items.Count == 0 ? 1 : items.Max(item => item.GetInt(_itemMetadata.GetAttribute("Sequence"))) + 1;

        var planReference = new EntityReference(_planMetadata.LogicalName, planEntity.Id);

        var batch = new ExecuteMultipleRequest
        {
            Settings = new ExecuteMultipleSettings
            {
                ContinueOnError = false,
                ReturnResponses = false,
            },
            Requests = new OrganizationRequestCollection(),
        };

        var created = 0;
        var updated = 0;

        foreach (var request in requests.GroupBy(r => r.ItemId).Select(group => group.Last()))
        {
            if (!lookup.TryGetValue(request.ItemId, out var entity))
            {
                throw new InvalidOperationException($"Invoice item {request.ItemId} not found in plan {planId}.");
            }

            var status = (InvoiceItemStatus)entity.GetInt(_itemMetadata.GetAttribute("Status"));
            if (status != InvoiceItemStatus.Requested)
            {
                throw new InvalidOperationException($"Invoice item {request.ItemId} must be Requested before it can be canceled.");
            }

            if (string.IsNullOrWhiteSpace(request.CancelReason))
            {
                throw new InvalidOperationException($"Cancel reason is required for invoice item {request.ItemId}.");
            }

            var percentage = entity.GetDecimal(_itemMetadata.GetAttribute("Percentage"));
            var amount = entity.GetDecimal(_itemMetadata.GetAttribute("Amount"));
            var payerCnpj = entity.GetString(_itemMetadata.GetAttribute("PayerCnpj"));
            var poNumber = entity.GetOptionalString(_itemMetadata.GetAttribute("PoNumber"));
            var frsNumber = entity.GetOptionalString(_itemMetadata.GetAttribute("FrsNumber"));
            var customerTicket = entity.GetOptionalString(_itemMetadata.GetAttribute("CustomerTicket"));
            var additionalInfo = entity.GetOptionalString(_itemMetadata.GetAttribute("AdditionalInfo"));
            var description = entity.GetOptionalString(_itemMetadata.GetAttribute("Description"));
            var emissionDate = request.ReplacementEmissionDate ?? entity.GetDateTime(_itemMetadata.GetAttribute("ScheduledDate"));
            var dueDate = request.ReplacementDueDate ?? (emissionDate?.AddDays(planEntity.GetInt(_planMetadata.GetAttribute("PaymentTermDays"))));

            var replacementId = nextItemSqlId++;
            var replacementEntity = new Entity(_itemMetadata.LogicalName)
            {
                [_itemMetadata.GetAttribute("InvoicePlan")] = planReference,
                [_itemMetadata.GetAttribute("InvoicePlanSqlId")] = planId,
                [_itemMetadata.GetAttribute("Sequence")] = nextSequence++,
                [_itemMetadata.GetAttribute("Description")] = description,
                [_itemMetadata.GetAttribute("Amount")] = amount,
                [_itemMetadata.GetAttribute("Percentage")] = percentage,
                [_itemMetadata.GetAttribute("ScheduledDate")] = emissionDate,
                [_itemMetadata.GetAttribute("DueDate")] = dueDate,
                [_itemMetadata.GetAttribute("PayerCnpj")] = payerCnpj,
                [_itemMetadata.GetAttribute("PoNumber")] = poNumber,
                [_itemMetadata.GetAttribute("FrsNumber")] = frsNumber,
                [_itemMetadata.GetAttribute("CustomerTicket")] = customerTicket,
                [_itemMetadata.GetAttribute("AdditionalInfo")] = additionalInfo,
                [_itemMetadata.GetAttribute("Status")] = new OptionSetValue((int)InvoiceItemStatus.Planned),
            };

            replacementEntity.KeyAttributes[_itemMetadata.GetAttribute("Id")] = replacementId;
            batch.Requests.Add(new UpsertRequest { Target = replacementEntity });

            var cancelUpdate = new Entity(_itemMetadata.LogicalName)
            {
                [_itemMetadata.GetAttribute("Status")] = new OptionSetValue((int)InvoiceItemStatus.Canceled),
                [_itemMetadata.GetAttribute("CanceledAt")] = DateTime.UtcNow.Date,
                [_itemMetadata.GetAttribute("CancelReason")] = request.CancelReason.Trim(),
                [_itemMetadata.GetAttribute("ReplacementItemSqlId")] = replacementId,
                [_itemMetadata.GetAttribute("BzCode")] = null,
                [_itemMetadata.GetAttribute("EmittedAt")] = null,
            };

            cancelUpdate.KeyAttributes[_itemMetadata.GetAttribute("Id")] = request.ItemId;
            batch.Requests.Add(new UpdateRequest { Target = cancelUpdate });

            created++;
            updated++;
        }

        if (batch.Requests.Count > 0)
        {
        await ExecuteAsync(client, batch, cancellationToken).ConfigureAwait(false);
        }

        return new RepositorySaveResult(created, updated, 0, created + updated);
    }

    private async Task<InvoiceSummaryResult> BuildSummaryAsync(ServiceClient client, InvoiceSummaryFilter filter, CancellationToken cancellationToken = default)
    {
        var query = new QueryExpression(_itemMetadata.LogicalName)
        {
            ColumnSet = new ColumnSet(
                _itemMetadata.GetAttribute("Id"),
                _itemMetadata.GetAttribute("InvoicePlanSqlId"),
                _itemMetadata.GetAttribute("Sequence"),
                _itemMetadata.GetAttribute("Amount"),
                _itemMetadata.GetAttribute("Percentage"),
                _itemMetadata.GetAttribute("ScheduledDate"),
                _itemMetadata.GetAttribute("DueDate"),
                _itemMetadata.GetAttribute("Status"),
                _itemMetadata.GetAttribute("RitmNumber"),
                _itemMetadata.GetAttribute("BzCode"),
                _itemMetadata.GetAttribute("RequestDate"),
                _itemMetadata.GetAttribute("EmittedAt"),
                _itemMetadata.GetAttribute("CanceledAt"),
                _itemMetadata.GetAttribute("CancelReason"))
        };

        if (filter.Statuses.Count > 0)
        {
            query.Criteria.AddCondition(_itemMetadata.GetAttribute("Status"), ConditionOperator.In, filter.Statuses.Select(status => (int)status).Cast<object>().ToArray());
        }

        var planLink = query.AddLink(_planMetadata.LogicalName, _itemMetadata.GetAttribute("InvoicePlan"), _planMetadata.PrimaryIdAttribute, JoinOperator.Inner);
        planLink.EntityAlias = "plan";
        planLink.Columns = new ColumnSet(
            _planMetadata.GetAttribute("Id"),
            _planMetadata.GetAttribute("EngagementId"),
            _planMetadata.GetAttribute("Type"),
            _planMetadata.GetAttribute("PaymentTermDays"));

        if (!string.IsNullOrWhiteSpace(filter.EngagementId))
        {
            planLink.LinkCriteria.AddCondition(_planMetadata.GetAttribute("EngagementId"), ConditionOperator.Equal, filter.EngagementId.Trim());
        }

        var engagementLink = planLink.AddLink(_engagementMetadata.LogicalName, _planMetadata.GetAttribute("Engagement"), _engagementMetadata.PrimaryIdAttribute, JoinOperator.LeftOuter);
        engagementLink.EntityAlias = "eng";
        engagementLink.Columns = new ColumnSet(
            _engagementMetadata.GetAttribute("SqlId"),
            _engagementMetadata.GetAttribute("Description"),
            _engagementMetadata.GetAttribute("CustomerSqlId"));

        if (filter.CustomerId.HasValue)
        {
            engagementLink.LinkCriteria.AddCondition(_engagementMetadata.GetAttribute("CustomerSqlId"), ConditionOperator.Equal, filter.CustomerId.Value);
        }

        var customerLink = engagementLink.AddLink(_customerMetadata.LogicalName, _engagementMetadata.GetAttribute("Customer"), _customerMetadata.PrimaryIdAttribute, JoinOperator.LeftOuter);
        customerLink.EntityAlias = "cust";
        customerLink.Columns = new ColumnSet(
            _customerMetadata.GetAttribute("SqlId"),
            _customerMetadata.GetAttribute("Name"),
            _customerMetadata.GetAttribute("CustomerCode"));

        var result = await RetrieveAsync(client, query, cancellationToken).ConfigureAwait(false);
        if (result.Entities.Count == 0)
        {
            return new InvoiceSummaryResult();
        }

        var groups = new List<InvoiceSummaryGroup>();
        var planAmounts = new Dictionary<int, decimal>();

        var groupedByPlan = result.Entities.GroupBy(entity => entity.GetAliasedInt("plan", _planMetadata.GetAttribute("Id")) ?? 0);
        foreach (var planGroup in groupedByPlan)
        {
            if (planGroup.Key == 0)
            {
                continue;
            }

            decimal total = 0m;
            foreach (var entity in planGroup)
            {
                total += entity.GetDecimal(_itemMetadata.GetAttribute("Amount"));
            }

            planAmounts[planGroup.Key] = total;
        }

        foreach (var engagementGroup in result.Entities.GroupBy(entity => new
                 {
                     PlanId = entity.GetAliasedInt("plan", _planMetadata.GetAttribute("Id")) ?? 0,
                     EngagementId = entity.GetAliasedString("plan", _planMetadata.GetAttribute("EngagementId")) ?? string.Empty,
                     EngagementName = entity.GetAliasedString("eng", _engagementMetadata.GetAttribute("Description")) ?? string.Empty,
                     CustomerId = entity.GetAliasedInt("eng", _engagementMetadata.GetAttribute("CustomerSqlId"))
                 }))
        {
            if (engagementGroup.Key.PlanId == 0)
            {
                continue;
            }

            var planId = engagementGroup.Key.PlanId;
            var engagementId = string.IsNullOrWhiteSpace(engagementGroup.Key.EngagementId)
                ? planId.ToString(CultureInfo.InvariantCulture)
                : engagementGroup.Key.EngagementId;

            var itemsForPlan = engagementGroup
                .OrderBy(entity => entity.GetInt(_itemMetadata.GetAttribute("Sequence")))
                .Select(entity => new InvoiceSummaryItem
                {
                    ItemId = entity.GetInt(_itemMetadata.GetAttribute("Id")),
                    PlanId = planId,
                    Sequence = entity.GetInt(_itemMetadata.GetAttribute("Sequence")),
                    PlanType = (InvoicePlanType)(entity.GetAliasedInt("plan", _planMetadata.GetAttribute("Type")) ?? 0),
                    Status = (InvoiceItemStatus)entity.GetInt(_itemMetadata.GetAttribute("Status")),
                    Percentage = entity.GetDecimal(_itemMetadata.GetAttribute("Percentage")),
                    Amount = entity.GetDecimal(_itemMetadata.GetAttribute("Amount")),
                    EmissionDate = entity.GetDateTime(_itemMetadata.GetAttribute("ScheduledDate"))?.Date,
                    DueDate = entity.GetDateTime(_itemMetadata.GetAttribute("DueDate"))?.Date,
                    RitmNumber = entity.GetOptionalString(_itemMetadata.GetAttribute("RitmNumber")),
                    BzCode = entity.GetOptionalString(_itemMetadata.GetAttribute("BzCode")),
                    RequestDate = entity.GetDateTime(_itemMetadata.GetAttribute("RequestDate"))?.Date,
                    EmittedAt = entity.GetDateTime(_itemMetadata.GetAttribute("EmittedAt"))?.Date,
                    CanceledAt = entity.GetDateTime(_itemMetadata.GetAttribute("CanceledAt"))?.Date,
                    CancelReason = entity.GetOptionalString(_itemMetadata.GetAttribute("CancelReason")),
                    BaseValue = planAmounts.TryGetValue(planId, out var baseValue) ? baseValue : (decimal?)null,
                })
                .ToList();

            var customerId = engagementGroup.Key.CustomerId;
            var customerName = string.Empty;
            var customerCode = string.Empty;

            if (customerId.HasValue)
            {
                foreach (var entity in engagementGroup)
                {
                    var sqlId = entity.GetAliasedInt("cust", _customerMetadata.GetAttribute("SqlId"));
                    if (sqlId == customerId)
                    {
                        customerName = entity.GetAliasedString("cust", _customerMetadata.GetAttribute("Name")) ?? string.Empty;
                        customerCode = entity.GetAliasedString("cust", _customerMetadata.GetAttribute("CustomerCode")) ?? string.Empty;
                        break;
                    }
                }
            }

            var group = new InvoiceSummaryGroup
            {
                EngagementId = engagementId,
                EngagementName = string.IsNullOrWhiteSpace(engagementGroup.Key.EngagementName) ? engagementId : engagementGroup.Key.EngagementName,
                CustomerId = customerId,
                CustomerName = string.IsNullOrWhiteSpace(customerName) ? null : customerName,
                CustomerCode = string.IsNullOrWhiteSpace(customerCode) ? null : customerCode,
                Items = itemsForPlan,
                TotalAmount = itemsForPlan.Sum(item => item.Amount),
                TotalPercentage = itemsForPlan.Sum(item => item.Percentage),
                PlannedCount = itemsForPlan.Count(item => item.Status == InvoiceItemStatus.Planned),
                RequestedCount = itemsForPlan.Count(item => item.Status == InvoiceItemStatus.Requested),
                ClosedCount = itemsForPlan.Count(item => item.Status == InvoiceItemStatus.Closed),
                CanceledCount = itemsForPlan.Count(item => item.Status == InvoiceItemStatus.Canceled),
                EmittedCount = itemsForPlan.Count(item => item.Status == InvoiceItemStatus.Emitted),
                ReissuedCount = itemsForPlan.Count(item => item.Status == InvoiceItemStatus.Reissued),
            };

            groups.Add(group);
        }

        return new InvoiceSummaryResult
        {
            Groups = groups,
            TotalAmount = groups.Sum(group => group.TotalAmount),
            TotalPercentage = groups.Sum(group => group.TotalPercentage),
        };
    }

    private async Task<IReadOnlyList<InvoiceNotificationPreview>> PreviewNotificationsInternalAsync(ServiceClient client, DateTime notificationDate, CancellationToken cancellationToken = default)
    {
        var targetDate = notificationDate.Date;
        var windowStart = targetDate.AddDays(7);
        var windowEnd = windowStart.AddDays(7);

        var query = new QueryExpression(_itemMetadata.LogicalName)
        {
            ColumnSet = new ColumnSet(
                _itemMetadata.GetAttribute("Id"),
                _itemMetadata.GetAttribute("InvoicePlanSqlId"),
                _itemMetadata.GetAttribute("Sequence"),
                _itemMetadata.GetAttribute("Amount"),
                _itemMetadata.GetAttribute("ScheduledDate"),
                _itemMetadata.GetAttribute("DueDate"),
                _itemMetadata.GetAttribute("Status"),
                _itemMetadata.GetAttribute("RitmNumber")),
            Criteria = new FilterExpression(LogicalOperator.And)
        };

        query.Criteria.AddCondition(_itemMetadata.GetAttribute("Status"), ConditionOperator.In, (int)InvoiceItemStatus.Planned, (int)InvoiceItemStatus.Requested);
        query.Criteria.AddCondition(_itemMetadata.GetAttribute("ScheduledDate"), ConditionOperator.OnOrAfter, windowStart);
        query.Criteria.AddCondition(_itemMetadata.GetAttribute("ScheduledDate"), ConditionOperator.OnOrBefore, windowEnd);

        var planLink = query.AddLink(_planMetadata.LogicalName, _itemMetadata.GetAttribute("InvoicePlan"), _planMetadata.PrimaryIdAttribute, JoinOperator.Inner);
        planLink.EntityAlias = "plan";
        planLink.Columns = new ColumnSet(
            _planMetadata.GetAttribute("Id"),
            _planMetadata.GetAttribute("EngagementId"),
            _planMetadata.GetAttribute("NumInvoices"),
            _planMetadata.GetAttribute("PaymentTermDays"),
            _planMetadata.GetAttribute("CustomerFocalPointName"),
            _planMetadata.GetAttribute("CustomerFocalPointEmail"));

        var engagementLink = planLink.AddLink(_engagementMetadata.LogicalName, _planMetadata.GetAttribute("Engagement"), _engagementMetadata.PrimaryIdAttribute, JoinOperator.LeftOuter);
        engagementLink.EntityAlias = "eng";
        engagementLink.Columns = new ColumnSet(
            _engagementMetadata.GetAttribute("SqlId"),
            _engagementMetadata.GetAttribute("Description"));

        var customerLink = engagementLink.AddLink(_customerMetadata.LogicalName, _engagementMetadata.GetAttribute("Customer"), _customerMetadata.PrimaryIdAttribute, JoinOperator.LeftOuter);
        customerLink.EntityAlias = "cust";
        customerLink.Columns = new ColumnSet(
            _customerMetadata.GetAttribute("Name"));

        var result = await RetrieveAsync(client, query, cancellationToken).ConfigureAwait(false);
        if (result.Entities.Count == 0)
        {
            return Array.Empty<InvoiceNotificationPreview>();
        }

        var previews = new List<InvoiceNotificationPreview>();
        var planIds = result.Entities
            .Select(entity => entity.GetAliasedInt("plan", _planMetadata.GetAttribute("Id")) ?? 0)
            .Where(id => id > 0)
            .Distinct()
            .ToArray();

        var emailLookup = await RetrieveEmailsForPlansAsync(client, planIds, cancellationToken).ConfigureAwait(false);

        foreach (var entity in result.Entities)
        {
            var emissionDate = entity.GetDateTime(_itemMetadata.GetAttribute("ScheduledDate"))?.Date;
            if (emissionDate is null)
            {
                continue;
            }

            if (CalculateNotificationDate(emissionDate.Value) != targetDate)
            {
                continue;
            }

            var planSqlId = entity.GetAliasedInt("plan", _planMetadata.GetAttribute("Id")) ?? 0;
            var engagementId = entity.GetAliasedString("plan", _planMetadata.GetAttribute("EngagementId")) ?? string.Empty;
            var planEmails = emailLookup.TryGetValue(planSqlId, out var emails)
                ? string.Join(';', emails.Select(email => email.GetString(_emailMetadata.GetAttribute("Email"))).Where(email => !string.IsNullOrWhiteSpace(email)))
                : string.Empty;

            var paymentTerm = entity.GetAliasedInt("plan", _planMetadata.GetAttribute("PaymentTermDays")) ?? 0;
            var computedDueDate = entity.GetDateTime(_itemMetadata.GetAttribute("DueDate"))?.Date
                ?? (emissionDate.Value.AddDays(paymentTerm));

            var preview = new InvoiceNotificationPreview
            {
                InvoiceItemId = entity.GetInt(_itemMetadata.GetAttribute("Id")),
                PlanId = planSqlId,
                EngagementId = string.IsNullOrWhiteSpace(engagementId) ? planSqlId.ToString(CultureInfo.InvariantCulture) : engagementId,
                SeqNo = entity.GetInt(_itemMetadata.GetAttribute("Sequence")),
                Amount = entity.GetDecimal(_itemMetadata.GetAttribute("Amount")),
                EmissionDate = emissionDate.Value,
                ComputedDueDate = computedDueDate,
                NotifyDate = targetDate,
                NumInvoices = entity.GetAliasedInt("plan", _planMetadata.GetAttribute("NumInvoices")) ?? 0,
                PaymentTermDays = paymentTerm,
                CustomerFocalPointName = entity.GetAliasedString("plan", _planMetadata.GetAttribute("CustomerFocalPointName")) ?? string.Empty,
                CustomerFocalPointEmail = entity.GetAliasedString("plan", _planMetadata.GetAttribute("CustomerFocalPointEmail")) ?? string.Empty,
                ExtraEmails = planEmails,
                CustomerName = entity.GetAliasedString("cust", _customerMetadata.GetAttribute("Name")) ?? string.Empty,
                ManagerEmails = string.Empty,
                ManagerNames = string.Empty,
            };

            previews.Add(preview);
        }

        EnrichPeople(previews);
        return previews
            .OrderBy(preview => preview.EngagementId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(preview => preview.SeqNo)
            .ToList();
    }

    private void EnrichPeople(IReadOnlyList<InvoiceNotificationPreview> previews)
    {
        if (previews.Count == 0)
        {
            return;
        }

        var identifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var preview in previews)
        {
            if (string.IsNullOrWhiteSpace(preview.CustomerFocalPointName) && !string.IsNullOrWhiteSpace(preview.CustomerFocalPointEmail))
            {
                identifiers.Add(preview.CustomerFocalPointEmail.Trim());
            }

            if (!string.IsNullOrWhiteSpace(preview.ManagerEmails))
            {
                foreach (var email in preview.ManagerEmails.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                {
                    identifiers.Add(email);
                }
            }
        }

        if (identifiers.Count == 0)
        {
            return;
        }

        IReadOnlyDictionary<string, string> resolved;
        try
        {
            resolved = _personDirectory.TryResolveDisplayNames(identifiers);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve Dataverse people metadata for notification preview.");
            return;
        }

        if (resolved.Count == 0)
        {
            return;
        }

        foreach (var preview in previews)
        {
            if (string.IsNullOrWhiteSpace(preview.CustomerFocalPointName) &&
                !string.IsNullOrWhiteSpace(preview.CustomerFocalPointEmail) &&
                resolved.TryGetValue(preview.CustomerFocalPointEmail.Trim(), out var focalName) &&
                !string.IsNullOrWhiteSpace(focalName))
            {
                preview.CustomerFocalPointName = focalName;
            }

            if (!string.IsNullOrWhiteSpace(preview.ManagerEmails))
            {
                var names = new List<string>();
                foreach (var email in preview.ManagerEmails.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                {
                    if (resolved.TryGetValue(email, out var display) && !string.IsNullOrWhiteSpace(display))
                    {
                        names.Add(display);
                    }
                }

                if (names.Count > 0)
                {
                    preview.ManagerNames = string.Join(';', names.Distinct(StringComparer.OrdinalIgnoreCase));
                }
            }
        }
    }

    private static DateTime CalculateNotificationDate(DateTime emissionDate)
    {
        var baseDate = emissionDate.Date.AddDays(-7);
        var daysToSubtract = ((int)baseDate.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return baseDate.AddDays(-daysToSubtract);
    }

    private async Task<int> GetNextSqlIdAsync(ServiceClient client, DataverseEntityMetadata metadata, CancellationToken cancellationToken)
    {
        var attribute = metadata.GetAttribute("Id");
        var query = new QueryExpression(metadata.LogicalName)
        {
            ColumnSet = new ColumnSet(attribute),
            Orders =
            {
                new OrderExpression(attribute, OrderType.Descending),
            },
            TopCount = 1,
        };

        var result = await RetrieveAsync(client, query, cancellationToken).ConfigureAwait(false);
        if (result.Entities.Count == 0)
        {
            return 1;
        }

        var current = result.Entities[0].GetInt(attribute);
        return current + 1;
    }
}
