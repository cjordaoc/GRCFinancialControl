using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Infrastructure;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GRCFinancialControl.Persistence.Services
{
    /// <summary>
    /// Manages planned hour allocations per engagement with fiscal year lock enforcement.
    /// </summary>
    public class PlannedAllocationService : IPlannedAllocationService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public PlannedAllocationService(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<List<PlannedAllocation>> GetAllocationsForEngagementAsync(int engagementId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            return await context.PlannedAllocations
                .AsNoTracking()
                .Where(pa => pa.EngagementId == engagementId)
                .Include(pa => pa.ClosingPeriod)
                .ToListAsync().ConfigureAwait(false);
        }

        public async Task SaveAllocationsForEngagementAsync(int engagementId, List<PlannedAllocation> allocations)
        {
            ArgumentNullException.ThrowIfNull(allocations);

            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);

            await EngagementMutationGuard.EnsureCanMutateAsync(
                context,
                engagementId,
                "Saving planned allocations").ConfigureAwait(false);

            var existingAllocations = await context.PlannedAllocations
                .Where(pa => pa.EngagementId == engagementId)
                .Include(pa => pa.ClosingPeriod)
                    .ThenInclude(cp => cp.FiscalYear)
                .ToListAsync().ConfigureAwait(false);

            var relevantClosingPeriodIds = existingAllocations
                .Select(a => a.ClosingPeriodId)
                .Concat(allocations.Select(a => a.ClosingPeriodId))
                .Distinct()
                .ToList();

            var closingPeriods = await context.ClosingPeriods
                .Include(cp => cp.FiscalYear)
                .Where(cp => relevantClosingPeriodIds.Contains(cp.Id))
                .ToDictionaryAsync(cp => cp.Id).ConfigureAwait(false);

            var lockedPeriods = closingPeriods.Values
                .Where(cp => cp.FiscalYear is not null && cp.FiscalYear.IsLocked)
                .ToList();

            var lockedPeriodIds = lockedPeriods
                .Select(cp => cp.Id)
                .ToHashSet();

            foreach (var lockedPeriod in lockedPeriods)
            {
                var existing = existingAllocations.FirstOrDefault(a => a.ClosingPeriodId == lockedPeriod.Id);
                var incoming = allocations.FirstOrDefault(a => a.ClosingPeriodId == lockedPeriod.Id);

                var fiscalYearName = string.IsNullOrWhiteSpace(lockedPeriod.FiscalYear?.Name)
                    ? $"Id={lockedPeriod.FiscalYear?.Id ?? 0}"
                    : lockedPeriod.FiscalYear!.Name;

                if (incoming is null)
                {
                    if (existing != null)
                    {
                        throw new InvalidOperationException($"Cannot remove planned allocation for locked fiscal year '{fiscalYearName}'. Unlock it before making changes.");
                    }

                    continue;
                }

                if (existing is null)
                {
                    throw new InvalidOperationException($"Cannot add a new planned allocation for locked fiscal year '{fiscalYearName}'. Unlock it before making changes.");
                }

                if (Math.Round(existing.AllocatedHours, 2, MidpointRounding.AwayFromZero) !=
                    Math.Round(incoming.AllocatedHours, 2, MidpointRounding.AwayFromZero))
                {
                    throw new InvalidOperationException($"Cannot change planned allocation hours for locked fiscal year '{fiscalYearName}'. Unlock it before making changes.");
                }
            }

            var unlockedPeriodIds = closingPeriods.Values
                .Where(cp => !(cp.FiscalYear?.IsLocked ?? false))
                .Select(cp => cp.Id)
                .ToHashSet();

            await context.PlannedAllocations
                .Where(pa => pa.EngagementId == engagementId && unlockedPeriodIds.Contains(pa.ClosingPeriodId))
                .ExecuteDeleteAsync().ConfigureAwait(false);

            foreach (var allocation in allocations.Where(a => unlockedPeriodIds.Contains(a.ClosingPeriodId)))
            {
                if (!closingPeriods.ContainsKey(allocation.ClosingPeriodId))
                {
                    throw new InvalidOperationException($"Closing period Id={allocation.ClosingPeriodId} could not be found. Refresh and try again.");
                }

                allocation.EngagementId = engagementId;
                await context.PlannedAllocations.AddAsync(new PlannedAllocation
                {
                    EngagementId = engagementId,
                    ClosingPeriodId = allocation.ClosingPeriodId,
                    AllocatedHours = allocation.AllocatedHours
                }).ConfigureAwait(false);
            }

            await context.SaveChangesAsync().ConfigureAwait(false);
        }
    }
}
