using System;
using System.Collections.Generic;
using System.Linq;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Invoices.Core.Enums;
using Invoices.Core.Models;
using Invoices.Core.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace Invoices.Data.Repositories;

public class InvoicePlanRepository : IInvoicePlanRepository
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly ILogger<InvoicePlanRepository> _logger;
    private readonly IPersonDirectory _personDirectory;
    private readonly IInvoiceAccessScope _accessScope;
    private readonly IDatabaseConnectionAvailability _connectionAvailability;

    public InvoicePlanRepository(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        ILogger<InvoicePlanRepository> logger,
        IPersonDirectory personDirectory,
        IInvoiceAccessScope accessScope,
        IDatabaseConnectionAvailability connectionAvailability)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _personDirectory = personDirectory ?? throw new ArgumentNullException(nameof(personDirectory));
        _accessScope = accessScope ?? throw new ArgumentNullException(nameof(accessScope));
        _connectionAvailability = connectionAvailability ?? throw new ArgumentNullException(nameof(connectionAvailability));
    }

    public InvoicePlan? GetPlan(int planId)
    {
        _accessScope.EnsureInitialized();

        if (_accessScope.IsInitialized && !_accessScope.HasAssignments && string.IsNullOrWhiteSpace(_accessScope.InitializationError))
        {
            return null;
        }

        using var context = CreateDbContext();

        var plan = context.InvoicePlans
            .Include(plan => plan.Items)
            .ThenInclude(item => item.Emissions)
            .Include(plan => plan.AdditionalEmails)
            .AsNoTracking()
            .FirstOrDefault(plan => plan.Id == planId);

        if (plan is null)
        {
            return null;
        }

        return _accessScope.IsEngagementAllowed(plan.EngagementId) ? plan : null;
    }

    public IReadOnlyList<InvoicePlan> ListPlansForEngagement(string engagementId)
    {
        if (string.IsNullOrWhiteSpace(engagementId))
        {
            return Array.Empty<InvoicePlan>();
        }

        _accessScope.EnsureInitialized();

        if (!_accessScope.IsEngagementAllowed(engagementId))
        {
            return Array.Empty<InvoicePlan>();
        }

        using var context = CreateDbContext();

        return context.InvoicePlans
            .Where(plan => plan.EngagementId == engagementId)
            .Include(plan => plan.Items)
            .ThenInclude(item => item.Emissions)
            .Include(plan => plan.AdditionalEmails)
            .AsNoTracking()
            .OrderBy(plan => plan.CreatedAt)
            .ToList();
    }

    public IReadOnlyList<EngagementLookup> ListEngagementsForPlanning()
    {
        if (!TryGetAccess(out var allowedEngagements, out var hasFilter))
        {
            return Array.Empty<EngagementLookup>();
        }

        using var context = CreateDbContext();

        var query = context.Engagements
            .AsNoTracking()
            .Include(engagement => engagement.Customer)
            .Where(engagement => !string.IsNullOrWhiteSpace(engagement.EngagementId));

        if (hasFilter)
        {
            query = query.Where(engagement => allowedEngagements.Contains(engagement.EngagementId));
        }

        return query
            .OrderBy(engagement => engagement.EngagementId)
            .Select(engagement => new EngagementLookup
            {
                Id = engagement.Id,
                EngagementId = engagement.EngagementId,
                Name = string.IsNullOrWhiteSpace(engagement.Description)
                    ? engagement.EngagementId
                    : engagement.Description,
                CustomerName = engagement.Customer == null
                    ? null
                    : engagement.Customer.Name,
                Currency = engagement.Currency,
            })
            .ToList();
    }

    public string? GetEngagementCurrency(string engagementId)
    {
        if (string.IsNullOrWhiteSpace(engagementId))
        {
            return null;
        }

        using var context = CreateDbContext();

        return context.Engagements
            .AsNoTracking()
            .Where(engagement => engagement.EngagementId == engagementId)
            .Select(engagement => engagement.Currency)
            .FirstOrDefault();
    }

    public IReadOnlyList<InvoicePlanSummary> ListPlansForRequestStage()
    {
        if (!TryGetAccess(out var allowedEngagements, out var hasFilter))
        {
            return Array.Empty<InvoicePlanSummary>();
        }

        using var context = CreateDbContext();

        var query = context.InvoicePlans.AsQueryable();

        if (hasFilter)
        {
            query = query.Where(plan => allowedEngagements.Contains(plan.EngagementId));
        }

        return ProjectSummaries(query.AsNoTracking(), context)
            .Where(summary => summary.PlannedItemCount > 0)
            .OrderByDescending(summary => summary.CreatedAt)
            .ToList();
    }

    public IReadOnlyList<InvoicePlanSummary> ListPlansForEmissionStage()
    {
        if (!TryGetAccess(out var allowedEngagements, out var hasFilter))
        {
            return Array.Empty<InvoicePlanSummary>();
        }

        using var context = CreateDbContext();

        var query = context.InvoicePlans.AsQueryable();

        if (hasFilter)
        {
            query = query.Where(plan => allowedEngagements.Contains(plan.EngagementId));
        }

        return ProjectSummaries(query.AsNoTracking(), context)
            .Where(summary => summary.RequestedItemCount > 0 || summary.EmittedItemCount > 0)
            .OrderByDescending(summary => summary.CreatedAt)
            .ToList();
    }

    public RepositorySaveResult SavePlan(InvoicePlan plan)
    {
        if (plan is null)
        {
            throw new ArgumentNullException(nameof(plan));
        }

        return ExecuteWithStrategy(context =>
        {
            using var transaction = context.Database.BeginTransaction();

            try
            {
                var now = DateTime.UtcNow;
                var created = 0;
                var updated = 0;
                var deleted = 0;

                if (plan.Id == 0)
                {
                    if (!_accessScope.IsEngagementAllowed(plan.EngagementId))
                    {
                        throw new InvalidOperationException(
                            $"The current user does not have access to engagement '{plan.EngagementId}'.");
                    }

                    PrepareNewPlan(plan, now);
                    context.InvoicePlans.Add(plan);
                    created = 1;
                }
                else
                {
                    var tracked = context.InvoicePlans
                        .Include(p => p.Items)
                        .Include(p => p.AdditionalEmails)
                        .FirstOrDefault(p => p.Id == plan.Id)
                        ?? throw new InvalidOperationException($"Invoice plan {plan.Id} not found.");

                    EnsurePlanAccess(tracked);

                    ApplyPlanUpdates(tracked, plan, now, context, ref deleted);
                    updated = 1;
                }

                var affectedRows = context.SaveChanges();
                transaction.Commit();

                return new RepositorySaveResult(created, updated, deleted, affectedRows);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Failed to persist invoice plan {PlanId}.", plan.Id);
                transaction.Rollback();
                throw;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        });
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

        return ExecuteWithStrategy(context =>
        {
            using var transaction = context.Database.BeginTransaction();

            try
            {
                var plan = context.InvoicePlans
                    .Include(p => p.Items)
                    .FirstOrDefault(p => p.Id == planId)
                    ?? throw new InvalidOperationException($"Invoice plan {planId} not found.");

                EnsurePlanAccess(plan);

                var now = DateTime.UtcNow;
                var updated = 0;
                var itemsById = plan.Items
                    .ToDictionary(item => item.Id);

                foreach (var update in updates
                             .GroupBy(u => u.ItemId)
                             .Select(group => group.Last()))
                {
                    if (!itemsById.TryGetValue(update.ItemId, out var item))
                    {
                        throw new InvalidOperationException($"Invoice item {update.ItemId} not found in plan {planId}.");
                    }

                    if (item.Status != InvoiceItemStatus.Planned)
                    {
                        throw new InvalidOperationException($"Invoice item {item.Id} is not in Planned status.");
                    }

                    if (string.IsNullOrWhiteSpace(update.RitmNumber))
                    {
                        throw new InvalidOperationException($"RITM number is required for item {item.Id}.");
                    }

                    if (string.IsNullOrWhiteSpace(update.CoeResponsible))
                    {
                        throw new InvalidOperationException($"COE responsible is required for item {item.Id}.");
                    }

                    if (update.RequestDate == default)
                    {
                        throw new InvalidOperationException($"Request date is required for item {item.Id}.");
                    }

                    item.RitmNumber = update.RitmNumber.Trim();
                    item.CoeResponsible = update.CoeResponsible.Trim();
                    item.RequestDate = update.RequestDate.Date;
                    item.Status = InvoiceItemStatus.Requested;
                    item.UpdatedAt = now;

                    updated++;
                }

                if (updated > 0)
                {
                    plan.UpdatedAt = now;
                }

                var affectedRows = context.SaveChanges();
                transaction.Commit();

                return new RepositorySaveResult(0, updated, 0, affectedRows);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Failed to mark request for plan {PlanId}.", planId);
                transaction.Rollback();
                throw;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        });
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

        return ExecuteWithStrategy(context =>
        {
            using var transaction = context.Database.BeginTransaction();

            try
            {
                var plan = context.InvoicePlans
                    .Include(p => p.Items)
                    .FirstOrDefault(p => p.Id == planId)
                    ?? throw new InvalidOperationException($"Invoice plan {planId} not found.");

                EnsurePlanAccess(plan);

                var now = DateTime.UtcNow;
                var updated = 0;
                var itemsById = plan.Items.ToDictionary(item => item.Id);

                foreach (var itemId in itemIds.Distinct())
                {
                    if (!itemsById.TryGetValue(itemId, out var item))
                    {
                        throw new InvalidOperationException($"Invoice item {itemId} not found in plan {planId}.");
                    }

                    if (item.Status != InvoiceItemStatus.Requested)
                    {
                        continue;
                    }

                    item.Status = InvoiceItemStatus.Planned;
                    item.RitmNumber = null;
                    item.CoeResponsible = null;
                    item.RequestDate = null;
                    item.UpdatedAt = now;
                    updated++;
                }

                if (updated > 0)
                {
                    plan.UpdatedAt = now;
                }

                var affectedRows = context.SaveChanges();
                transaction.Commit();

                return new RepositorySaveResult(0, updated, 0, affectedRows);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Failed to undo request for plan {PlanId}.", planId);
                transaction.Rollback();
                throw;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        });
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

        return ExecuteWithStrategy(context =>
        {
            using var transaction = context.Database.BeginTransaction();

            try
            {
                var plan = context.InvoicePlans
                    .Include(p => p.Items)
                    .FirstOrDefault(p => p.Id == planId)
                    ?? throw new InvalidOperationException($"Invoice plan {planId} not found.");

                EnsurePlanAccess(plan);

                var now = DateTime.UtcNow;
                var updated = 0;
                var itemsById = plan.Items.ToDictionary(item => item.Id);

                foreach (var update in updates
                             .GroupBy(u => u.ItemId)
                             .Select(group => group.Last()))
                {
                    if (!itemsById.TryGetValue(update.ItemId, out var item))
                    {
                        throw new InvalidOperationException($"Invoice item {update.ItemId} not found in plan {planId}.");
                    }

                    if (item.Status != InvoiceItemStatus.Requested)
                    {
                        throw new InvalidOperationException($"Invoice item {item.Id} is not in Requested status.");
                    }

                    if (string.IsNullOrWhiteSpace(item.RitmNumber))
                    {
                        throw new InvalidOperationException($"Invoice item {item.Id} cannot be closed without a RITM.");
                    }

                    if (string.IsNullOrWhiteSpace(update.BzCode))
                    {
                        throw new InvalidOperationException($"BZ code is required to close invoice item {item.Id}.");
                    }

                    var emittedDate = update.EmittedAt?.Date ?? DateTime.UtcNow.Date;

                    var emission = new InvoiceEmission
                    {
                        InvoiceItemId = item.Id,
                        BzCode = update.BzCode.Trim(),
                        EmittedAt = emittedDate,
                        CreatedAt = now,
                        UpdatedAt = now,
                    };

                    context.InvoiceEmissions.Add(emission);

                    item.Status = InvoiceItemStatus.Emitted;
                    item.UpdatedAt = now;

                    updated++;
                }

                if (updated > 0)
                {
                    plan.UpdatedAt = now;
                }

                var affectedRows = context.SaveChanges();
                transaction.Commit();

                return new RepositorySaveResult(0, updated, 0, affectedRows);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Failed to close invoice items for plan {PlanId}.", planId);
                transaction.Rollback();
                throw;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        });
    }

    public RepositorySaveResult CancelEmissions(int planId, IReadOnlyCollection<InvoiceEmissionCancellation> cancellations)
    {
        if (cancellations is null)
        {
            throw new ArgumentNullException(nameof(cancellations));
        }

        if (cancellations.Count == 0)
        {
            return RepositorySaveResult.Empty;
        }

        return ExecuteWithStrategy(context =>
        {
            using var transaction = context.Database.BeginTransaction();

            try
            {
                var plan = context.InvoicePlans
                    .Include(p => p.Items)
                    .ThenInclude(item => item.Emissions)
                    .FirstOrDefault(p => p.Id == planId)
                    ?? throw new InvalidOperationException($"Invoice plan {planId} not found.");

                EnsurePlanAccess(plan);

                var now = DateTime.UtcNow;
                var updated = 0;
                var itemsById = plan.Items.ToDictionary(item => item.Id);

                foreach (var cancellation in cancellations
                             .GroupBy(request => request.ItemId)
                             .Select(group => group.Last()))
                {
                    if (!itemsById.TryGetValue(cancellation.ItemId, out var item))
                    {
                        throw new InvalidOperationException($"Invoice item {cancellation.ItemId} not found in plan {planId}.");
                    }

                    if (item.Status != InvoiceItemStatus.Emitted)
                    {
                        throw new InvalidOperationException($"Invoice item {item.Id} is not emitted.");
                    }

                    if (string.IsNullOrWhiteSpace(cancellation.CancelReason))
                    {
                        throw new InvalidOperationException($"Cancel reason is required for invoice item {item.Id}.");
                    }

                    var emission = item.Emissions
                        .OrderByDescending(entry => entry.EmittedAt)
                        .FirstOrDefault(entry => entry.CanceledAt == null);

                    if (emission is null)
                    {
                        throw new InvalidOperationException($"No active emission found for invoice item {item.Id}.");
                    }

                    emission.CanceledAt = cancellation.CanceledAt?.Date ?? now.Date;
                    emission.CancelReason = cancellation.CancelReason.Trim();
                    emission.UpdatedAt = now;

                    item.Status = InvoiceItemStatus.Planned;
                    item.RitmNumber = null;
                    item.CoeResponsible = null;
                    item.RequestDate = null;
                    item.UpdatedAt = now;

                    updated++;
                }

                if (updated > 0)
                {
                    plan.UpdatedAt = now;
                }

                var affectedRows = context.SaveChanges();

                transaction.Commit();

                return new RepositorySaveResult(0, updated, 0, affectedRows);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Failed to cancel invoice emissions for plan {PlanId}.", planId);
                transaction.Rollback();
                throw;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        });
    }

    public RepositorySaveResult DeletePlan(int planId)
    {
        if (planId <= 0)
        {
            return RepositorySaveResult.Empty;
        }

        return ExecuteWithStrategy(context =>
        {
            var plan = context.InvoicePlans
                .Include(p => p.Items)
                .Include(p => p.AdditionalEmails)
                .FirstOrDefault(p => p.Id == planId);

            if (plan is null)
            {
                return RepositorySaveResult.Empty;
            }

            EnsurePlanAccess(plan);

            context.InvoicePlans.Remove(plan);

            var affectedRows = context.SaveChanges();

            return new RepositorySaveResult(0, 0, 1, affectedRows);
        });
    }

    public InvoiceSummaryResult SearchSummary(InvoiceSummaryFilter filter)
    {
        if (filter is null)
        {
            throw new ArgumentNullException(nameof(filter));
        }

        if (!TryGetAccess(out var allowedEngagements, out var hasFilter))
        {
            return new InvoiceSummaryResult();
        }

        using var context = CreateDbContext();

        var statuses = (filter.Statuses ?? Array.Empty<InvoiceItemStatus>())
            .Where(status => Enum.IsDefined(typeof(InvoiceItemStatus), status))
            .Distinct()
            .ToArray();

        var query = from plan in context.InvoicePlans.AsNoTracking()
                    join engagement in context.Engagements.AsNoTracking() on plan.EngagementId equals engagement.EngagementId into engagementJoin
                    from engagement in engagementJoin.DefaultIfEmpty()
                    join item in context.InvoiceItems.AsNoTracking() on plan.Id equals item.PlanId
                    select new
                    {
                        Plan = plan,
                        Item = item,
                        Engagement = engagement,
                    };

        if (hasFilter)
        {
            query = query.Where(row => allowedEngagements.Contains(row.Plan.EngagementId));
        }

        if (!string.IsNullOrWhiteSpace(filter.EngagementId))
        {
            query = query.Where(row => row.Plan.EngagementId == filter.EngagementId);
        }

        if (filter.CustomerId.HasValue)
        {
            var customerId = filter.CustomerId.Value;
            query = query.Where(row => row.Engagement != null && row.Engagement.CustomerId == customerId);
        }

        if (statuses.Length > 0)
        {
            query = query.Where(row => statuses.Contains(row.Item.Status));
        }

        var rows = query.ToList();

        if (rows.Count == 0)
        {
            return new InvoiceSummaryResult();
        }

        var customerIds = rows
            .Select(row => row.Engagement?.CustomerId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();

        var customersById = customerIds.Length == 0
            ? new Dictionary<int, Customer>()
            : context.Customers
                .AsNoTracking()
                .Where(customer => customerIds.Contains(customer.Id))
                .ToDictionary(customer => customer.Id);

        var itemIds = rows
            .Select(row => row.Item.Id)
            .Distinct()
            .ToArray();

        var emissions = itemIds.Length == 0
            ? new List<InvoiceEmission>()
            : context.InvoiceEmissions
                .AsNoTracking()
                .Where(emission => itemIds.Contains(emission.InvoiceItemId))
                .ToList();

        var activeEmissions = emissions
            .Where(emission => emission.CanceledAt == null)
            .GroupBy(emission => emission.InvoiceItemId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(emission => emission.EmittedAt)
                    .ThenByDescending(emission => emission.Id)
                    .First());

        var lastCanceledEmissions = emissions
            .Where(emission => emission.CanceledAt != null)
            .GroupBy(emission => emission.InvoiceItemId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(emission => emission.CanceledAt)
                    .ThenByDescending(emission => emission.Id)
                    .First());

        var planBaseAmounts = rows
            .GroupBy(row => row.Plan.Id)
            .ToDictionary(group => group.Key, group => group.Sum(row => row.Item.Amount));

        var groups = new List<InvoiceSummaryGroup>();

        foreach (var engagementGroup in rows
                     .GroupBy(row => new
                     {
                         row.Plan.EngagementId,
                         EngagementName = row.Engagement?.Description ?? row.Plan.EngagementId,
                         row.Engagement?.CustomerId,
                     })
                     .OrderBy(group => group.Key.CustomerId)
                     .ThenBy(group => group.Key.EngagementName))
        {
            Customer? customer = null;
            if (engagementGroup.Key.CustomerId.HasValue)
            {
                customersById.TryGetValue(engagementGroup.Key.CustomerId.Value, out customer);
            }

            var items = engagementGroup
                .OrderBy(row => row.Plan.Id)
                .ThenBy(row => row.Item.SeqNo)
                .Select(row =>
                {
                    activeEmissions.TryGetValue(row.Item.Id, out var activeEmission);
                    lastCanceledEmissions.TryGetValue(row.Item.Id, out var canceledEmission);

                    return new InvoiceSummaryItem
                    {
                        ItemId = row.Item.Id,
                        PlanId = row.Plan.Id,
                        Sequence = row.Item.SeqNo,
                        PlanType = row.Plan.Type,
                        Status = row.Item.Status,
                        Percentage = row.Item.Percentage,
                        Amount = row.Item.Amount,
                        EmissionDate = row.Item.EmissionDate,
                        DueDate = row.Item.DueDate,
                        RitmNumber = row.Item.RitmNumber,
                        BzCode = activeEmission?.BzCode,
                        RequestDate = row.Item.RequestDate,
                        EmittedAt = activeEmission?.EmittedAt,
                        CanceledAt = canceledEmission?.CanceledAt,
                        CancelReason = canceledEmission?.CancelReason,
                        BaseValue = planBaseAmounts.TryGetValue(row.Plan.Id, out var baseValue) ? baseValue : null,
                    };
                })
                .ToList();

            var group = new InvoiceSummaryGroup
            {
                EngagementId = engagementGroup.Key.EngagementId,
                EngagementName = engagementGroup.Key.EngagementName,
                CustomerName = customer?.Name,
                CustomerCode = customer?.CustomerCode,
                CustomerId = customer?.Id,
                TotalAmount = items.Sum(item => item.Amount),
                TotalPercentage = items.Sum(item => item.Percentage),
                PlannedCount = items.Count(item => item.Status == InvoiceItemStatus.Planned),
                RequestedCount = items.Count(item => item.Status == InvoiceItemStatus.Requested),
                ClosedCount = items.Count(item => item.Status == InvoiceItemStatus.Closed),
                CanceledCount = items.Count(item => item.Status == InvoiceItemStatus.Canceled),
                EmittedCount = items.Count(item => item.Status == InvoiceItemStatus.Emitted),
                ReissuedCount = items.Count(item => item.Status == InvoiceItemStatus.Reissued),
                Items = items,
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

    public IReadOnlyList<InvoiceNotificationPreview> PreviewNotifications(DateTime notificationDate)
    {
        if (!TryGetAccess(out var allowedEngagements, out var hasFilter))
        {
            return Array.Empty<InvoiceNotificationPreview>();
        }

        using var context = CreateDbContext();

        var targetDate = notificationDate.Date;

        var query = context.Set<InvoiceNotificationPreview>()
            .FromSqlInterpolated($"SELECT * FROM vw_InvoiceNotifyOnDate WHERE NotifyDate = DATE({targetDate})")
            .AsNoTracking();

        if (hasFilter)
        {
            query = query.Where(entry => allowedEngagements.Contains(entry.EngagementId));
        }

        var previews = query
            .OrderBy(entry => entry.EngagementId)
            .ThenBy(entry => entry.SeqNo)
            .ToList();

        EnrichPeople(previews);

        return previews;
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
            if (string.IsNullOrWhiteSpace(preview.CustomerFocalPointName) &&
                !string.IsNullOrWhiteSpace(preview.CustomerFocalPointEmail))
            {
                identifiers.Add(preview.CustomerFocalPointEmail.Trim());
            }

            if (string.IsNullOrWhiteSpace(preview.ManagerNames) && !string.IsNullOrWhiteSpace(preview.ManagerEmails))
            {
                foreach (var email in preview.ManagerEmails
                             .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (!string.IsNullOrWhiteSpace(email))
                    {
                        identifiers.Add(email);
                    }
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
            _logger.LogWarning(ex, "Failed to resolve person metadata for notification preview.");
            return;
        }

        if (resolved.Count == 0)
        {
            return;
        }

        foreach (var preview in previews)
        {
            if (string.IsNullOrWhiteSpace(preview.CustomerFocalPointName) &&
                !string.IsNullOrWhiteSpace(preview.CustomerFocalPointEmail))
            {
                var email = preview.CustomerFocalPointEmail.Trim();
                if (resolved.TryGetValue(email, out var displayName) && !string.IsNullOrWhiteSpace(displayName))
                {
                    preview.CustomerFocalPointName = displayName;
                }
            }

            if (string.IsNullOrWhiteSpace(preview.ManagerNames) && !string.IsNullOrWhiteSpace(preview.ManagerEmails))
            {
                var names = new List<string>();
                foreach (var email in preview.ManagerEmails
                             .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (resolved.TryGetValue(email, out var displayName) && !string.IsNullOrWhiteSpace(displayName))
                    {
                        names.Add(displayName);
                    }
                }

                if (names.Count > 0)
                {
                    preview.ManagerNames = string.Join(';', names.Distinct(StringComparer.OrdinalIgnoreCase));
                }
            }
        }
    }

    private static IQueryable<InvoicePlanSummary> ProjectSummaries(IQueryable<InvoicePlan> query, ApplicationDbContext context)
    {
        return from plan in query
               join engagement in context.Engagements.Include(e => e.Customer).AsNoTracking() on plan.EngagementId equals engagement.EngagementId into engagementJoin
               from engagement in engagementJoin.DefaultIfEmpty()
               select new InvoicePlanSummary
               {
                   Id = plan.Id,
                   EngagementId = plan.EngagementId,
                   EngagementDescription = engagement != null ? engagement.Description : string.Empty,
                   CustomerName = engagement != null && engagement.Customer != null ? engagement.Customer.Name : null,
                   Type = plan.Type,
                   CreatedAt = plan.CreatedAt,
                   FirstEmissionDate = plan.FirstEmissionDate,
                   PlannedItemCount = plan.Items.Count(item => item.Status == InvoiceItemStatus.Planned),
                   RequestedItemCount = plan.Items.Count(item => item.Status == InvoiceItemStatus.Requested),
                   EmittedItemCount = plan.Items.Count(item => item.Status == InvoiceItemStatus.Emitted),
                   ClosedItemCount = plan.Items.Count(item => item.Status == InvoiceItemStatus.Closed),
                   CanceledItemCount = plan.Items.Count(item => item.Status == InvoiceItemStatus.Canceled),
               };
    }

    private bool TryGetAccess(out string[] allowedEngagements, out bool hasFilter)
    {
        _accessScope.EnsureInitialized();

        hasFilter = _accessScope.HasAssignments;
        allowedEngagements = hasFilter
            ? _accessScope.EngagementIds.ToArray()
            : Array.Empty<string>();

        return !(_accessScope.IsInitialized
                 && !hasFilter
                 && string.IsNullOrWhiteSpace(_accessScope.InitializationError));
    }

    private ApplicationDbContext CreateDbContext()
    {
        if (_connectionAvailability.IsConfigured)
        {
            return _dbContextFactory.CreateDbContext();
        }

        var message = _connectionAvailability.ErrorMessage
            ?? "Connection settings are missing or incomplete.";
        throw new InvalidOperationException(message);
    }

    private T ExecuteWithStrategy<T>(Func<ApplicationDbContext, T> operation)
    {
        using var strategyContext = CreateDbContext();
        var strategy = strategyContext.Database.CreateExecutionStrategy();

        return strategy.Execute(() =>
        {
            using var context = CreateDbContext();
            return operation(context);
        });
    }

    private void EnsurePlanAccess(InvoicePlan plan)
    {
        if (plan is null)
        {
            throw new ArgumentNullException(nameof(plan));
        }

        if (!_accessScope.IsEngagementAllowed(plan.EngagementId))
        {
            throw new InvalidOperationException(
                $"The current user does not have access to engagement '{plan.EngagementId}'.");
        }
    }

    private static void PrepareNewPlan(InvoicePlan plan, DateTime utcNow)
    {
        plan.CreatedAt = utcNow;
        plan.UpdatedAt = utcNow;

        foreach (var item in plan.Items)
        {
            item.Status = InvoiceItemStatus.Planned;
            item.CreatedAt = utcNow;
            item.UpdatedAt = utcNow;
            item.Plan = null;
            item.RitmNumber = null;
            item.CoeResponsible = null;
            item.RequestDate = null;
            item.Emissions.Clear();
        }

        foreach (var email in plan.AdditionalEmails)
        {
            email.CreatedAt = utcNow;
            email.Plan = null;
        }
    }

    private static void ApplyPlanUpdates(
        InvoicePlan tracked,
        InvoicePlan incoming,
        DateTime utcNow,
        ApplicationDbContext context,
        ref int deleted)
    {
        tracked.EngagementId = incoming.EngagementId;
        tracked.Type = incoming.Type;
        tracked.NumInvoices = incoming.NumInvoices;
        tracked.PaymentTermDays = incoming.PaymentTermDays;
        tracked.CustomerFocalPointName = incoming.CustomerFocalPointName;
        tracked.CustomerFocalPointEmail = incoming.CustomerFocalPointEmail;
        tracked.CustomInstructions = incoming.CustomInstructions;
        tracked.FirstEmissionDate = incoming.FirstEmissionDate;
        tracked.UpdatedAt = utcNow;

        UpdateItems(tracked, incoming, utcNow, context, ref deleted);
        UpdateEmails(tracked, incoming, utcNow, context, ref deleted);
    }

    private static void UpdateItems(
        InvoicePlan tracked,
        InvoicePlan incoming,
        DateTime utcNow,
        ApplicationDbContext context,
        ref int deleted)
    {
        var existingById = tracked.Items.ToDictionary(item => item.Id);
        var retainedIds = new HashSet<int>();

        foreach (var incomingItem in incoming.Items)
        {
            if (incomingItem.Id != 0 && existingById.TryGetValue(incomingItem.Id, out var trackedItem))
            {
                trackedItem.SeqNo = incomingItem.SeqNo;
                trackedItem.Percentage = incomingItem.Percentage;
                trackedItem.Amount = incomingItem.Amount;
                trackedItem.EmissionDate = incomingItem.EmissionDate;
                trackedItem.DueDate = incomingItem.DueDate;
                trackedItem.PayerCnpj = incomingItem.PayerCnpj;
                trackedItem.PoNumber = incomingItem.PoNumber;
                trackedItem.FrsNumber = incomingItem.FrsNumber;
                trackedItem.CustomerTicket = incomingItem.CustomerTicket;
                trackedItem.AdditionalInfo = incomingItem.AdditionalInfo;
                trackedItem.DeliveryDescription = incomingItem.DeliveryDescription;
                trackedItem.PaymentTypeCode = PaymentTypeCatalog.NormalizeCode(incomingItem.PaymentTypeCode);
                trackedItem.UpdatedAt = utcNow;
                retainedIds.Add(trackedItem.Id);
            }
            else
            {
                incomingItem.PlanId = tracked.Id;
                incomingItem.Plan = tracked;
                incomingItem.Status = InvoiceItemStatus.Planned;
                incomingItem.CreatedAt = utcNow;
                incomingItem.UpdatedAt = utcNow;
                incomingItem.RitmNumber = null;
                incomingItem.CoeResponsible = null;
                incomingItem.RequestDate = null;
                incomingItem.PaymentTypeCode = PaymentTypeCatalog.NormalizeCode(incomingItem.PaymentTypeCode);
                incomingItem.Emissions.Clear();
                tracked.Items.Add(incomingItem);
                context.Entry(incomingItem).State = EntityState.Added;
                retainedIds.Add(incomingItem.Id);
            }
        }

        foreach (var trackedItem in tracked.Items.ToList())
        {
            if (!retainedIds.Contains(trackedItem.Id))
            {
                context.InvoiceItems.Remove(trackedItem);
                deleted++;
            }
        }
    }

    private static void UpdateEmails(
        InvoicePlan tracked,
        InvoicePlan incoming,
        DateTime utcNow,
        ApplicationDbContext context,
        ref int deleted)
    {
        var existingById = tracked.AdditionalEmails.ToDictionary(email => email.Id);
        var retainedIds = new HashSet<int>();

        foreach (var incomingEmail in incoming.AdditionalEmails)
        {
            if (incomingEmail.Id != 0 && existingById.TryGetValue(incomingEmail.Id, out var trackedEmail))
            {
                trackedEmail.Email = incomingEmail.Email;
                retainedIds.Add(trackedEmail.Id);
            }
            else
            {
                incomingEmail.PlanId = tracked.Id;
                incomingEmail.Plan = tracked;
                incomingEmail.CreatedAt = utcNow;
                tracked.AdditionalEmails.Add(incomingEmail);
                context.Entry(incomingEmail).State = EntityState.Added;
                retainedIds.Add(incomingEmail.Id);
            }
        }

        foreach (var trackedEmail in tracked.AdditionalEmails.ToList())
        {
            if (!retainedIds.Contains(trackedEmail.Id))
            {
                context.InvoicePlanEmails.Remove(trackedEmail);
                deleted++;
            }
        }
    }
}
