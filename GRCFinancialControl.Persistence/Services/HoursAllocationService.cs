using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GRCFinancialControl.Persistence.Services
{
    public sealed class HoursAllocationService : IHoursAllocationService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public HoursAllocationService(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        }

        public async Task<HoursAllocationSnapshot> GetAllocationAsync(int engagementId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);

            var engagement = await context.Engagements
                .AsNoTracking()
                .Include(e => e.RankBudgets)
                    .ThenInclude(b => b.FiscalYear)
                .FirstOrDefaultAsync(e => e.Id == engagementId)
                .ConfigureAwait(false);

            if (engagement is null)
            {
                throw new InvalidOperationException($"Engagement {engagementId} was not found.");
            }

            var fiscalYears = await context.FiscalYears
                .AsNoTracking()
                .OrderBy(fy => fy.IsLocked)
                .ThenBy(fy => fy.StartDate)
                .ToListAsync()
                .ConfigureAwait(false);

            var fiscalYearInfos = fiscalYears
                .Select(fy => new FiscalYearAllocationInfo(fy.Id, fy.Name, fy.IsLocked))
                .ToList();

            var rows = BuildRows(fiscalYears, engagement.RankBudgets);

            var consumedInOpenYears = engagement.RankBudgets
                .Where(budget => !(budget.FiscalYear?.IsLocked ?? false))
                .Sum(budget => budget.ConsumedHours);

            var actualHours = engagement.EstimatedToCompleteHours;
            var toBeConsumed = actualHours - consumedInOpenYears;

            return new HoursAllocationSnapshot(
                engagement.Id,
                engagement.EngagementId,
                engagement.Description,
                engagement.InitialHoursBudget,
                actualHours,
                toBeConsumed,
                fiscalYearInfos,
                rows);
        }

        public async Task<HoursAllocationSnapshot> SaveAsync(int engagementId, IEnumerable<HoursAllocationCellUpdate> updates)
        {
            if (updates is null)
            {
                throw new ArgumentNullException(nameof(updates));
            }

            var updateList = updates.ToList();
            if (updateList.Count == 0)
            {
                return await GetAllocationAsync(engagementId).ConfigureAwait(false);
            }

            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);

            var updateIds = updateList.Select(update => update.BudgetId).ToList();

            var budgets = await context.EngagementRankBudgets
                .Include(b => b.FiscalYear)
                .Where(b => b.EngagementId == engagementId && updateIds.Contains(b.Id))
                .ToListAsync()
                .ConfigureAwait(false);

            if (budgets.Count != updateList.Count)
            {
                throw new InvalidOperationException("One or more allocation entries could not be found for update.");
            }

            var budgetsById = budgets.ToDictionary(b => b.Id);

            foreach (var update in updateList)
            {
                if (!budgetsById.TryGetValue(update.BudgetId, out var budget))
                {
                    throw new InvalidOperationException($"Allocation budget {update.BudgetId} could not be found for engagement {engagementId}.");
                }

                if (budget.FiscalYear?.IsLocked ?? false)
                {
                    throw new InvalidOperationException($"Fiscal year '{budget.FiscalYear.Name}' is locked. Unlock it before adjusting consumed hours.");
                }

                budget.ConsumedHours = Math.Round(update.ConsumedHours, 2, MidpointRounding.AwayFromZero);
            }

            await context.SaveChangesAsync().ConfigureAwait(false);

            return await GetAllocationAsync(engagementId).ConfigureAwait(false);
        }

        public async Task<HoursAllocationSnapshot> AddRankAsync(int engagementId, string rankName)
        {
            var normalizedRank = NormalizeRank(rankName);
            if (string.IsNullOrEmpty(normalizedRank))
            {
                throw new InvalidOperationException("The rank name must be provided.");
            }

            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);

            var existingRanks = await context.EngagementRankBudgets
                .Where(b => b.EngagementId == engagementId)
                .Select(b => b.RankName)
                .ToListAsync()
                .ConfigureAwait(false);

            if (existingRanks.Any(r => string.Equals(r, normalizedRank, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"The rank '{normalizedRank}' is already registered for this engagement.");
            }

            var openFiscalYears = await context.FiscalYears
                .AsNoTracking()
                .Where(fy => !fy.IsLocked)
                .Select(fy => fy.Id)
                .ToListAsync()
                .ConfigureAwait(false);

            if (openFiscalYears.Count == 0)
            {
                throw new InvalidOperationException("There are no open fiscal years available to create the rank allocation.");
            }

            foreach (var fiscalYearId in openFiscalYears)
            {
                context.EngagementRankBudgets.Add(new EngagementRankBudget
                {
                    EngagementId = engagementId,
                    FiscalYearId = fiscalYearId,
                    RankName = normalizedRank,
                    BudgetHours = 0m,
                    ConsumedHours = 0m
                });
            }

            await context.SaveChangesAsync().ConfigureAwait(false);

            return await GetAllocationAsync(engagementId).ConfigureAwait(false);
        }

        public async Task DeleteRankAsync(int engagementId, string rankName)
        {
            var normalizedRank = NormalizeRank(rankName);
            if (string.IsNullOrEmpty(normalizedRank))
            {
                throw new InvalidOperationException("The rank name must be provided.");
            }

            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);

            var rankBudgets = await context.EngagementRankBudgets
                .Include(b => b.FiscalYear)
                .Where(b => b.EngagementId == engagementId)
                .ToListAsync()
                .ConfigureAwait(false);

            var budgetsToRemove = rankBudgets
                .Where(b => string.Equals(b.RankName, normalizedRank, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (budgetsToRemove.Count == 0)
            {
                return;
            }

            if (budgetsToRemove.Any(b => Math.Round(b.BudgetHours, 2, MidpointRounding.AwayFromZero) != 0m ||
                                         Math.Round(b.ConsumedHours, 2, MidpointRounding.AwayFromZero) != 0m))
            {
                throw new InvalidOperationException($"The rank '{normalizedRank}' cannot be removed because it has non-zero budget or consumed hours.");
            }

            context.EngagementRankBudgets.RemoveRange(budgetsToRemove);
            await context.SaveChangesAsync().ConfigureAwait(false);
        }

        private static List<HoursAllocationRowSnapshot> BuildRows(
            IReadOnlyList<FiscalYear> fiscalYears,
            IEnumerable<EngagementRankBudget> budgets)
        {
            var fiscalYearLookup = fiscalYears.ToDictionary(fy => fy.Id);

            var groupedBudgets = budgets
                .GroupBy(b => b.RankName, StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var rows = new List<HoursAllocationRowSnapshot>(groupedBudgets.Count);

            foreach (var group in groupedBudgets)
            {
                var cells = new List<HoursAllocationCellSnapshot>(fiscalYears.Count);
                foreach (var fiscalYear in fiscalYears)
                {
                    var match = group.FirstOrDefault(b => b.FiscalYearId == fiscalYear.Id);
                    if (match is null)
                    {
                        cells.Add(new HoursAllocationCellSnapshot(
                            null,
                            fiscalYear.Id,
                            0m,
                            0m,
                            0m,
                            fiscalYear.IsLocked));
                    }
                    else
                    {
                        var isLocked = match.FiscalYear?.IsLocked ?? fiscalYearLookup[fiscalYear.Id].IsLocked;
                        var remainingHours = Math.Round(match.BudgetHours - match.ConsumedHours, 2, MidpointRounding.AwayFromZero);
                        cells.Add(new HoursAllocationCellSnapshot(
                            match.Id,
                            match.FiscalYearId,
                            match.BudgetHours,
                            match.ConsumedHours,
                            remainingHours,
                            isLocked));
                    }
                }

                rows.Add(new HoursAllocationRowSnapshot(group.Key, cells));
            }

            return rows;
        }

        private static string NormalizeRank(string value)
        {
            return value?.Trim() ?? string.Empty;
        }
    }
}
