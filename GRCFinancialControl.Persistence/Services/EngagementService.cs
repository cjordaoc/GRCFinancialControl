using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Enums;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Infrastructure;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using static GRCFinancialControl.Persistence.Services.ClosingPeriodIdHelper;

namespace GRCFinancialControl.Persistence.Services
{
    public class EngagementService : IEngagementService
    {
        private const string FinancialEvolutionInitialPeriodId = "INITIAL";

        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public EngagementService(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            ArgumentNullException.ThrowIfNull(contextFactory);

            _contextFactory = contextFactory;
        }

        public async Task<List<Engagement>> GetAllAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            ArgumentNullException.ThrowIfNull(context);

            var closingPeriodRecords = await context.ClosingPeriods
                .AsNoTracking()
                .Where(cp => !string.IsNullOrWhiteSpace(cp.Name))
                .ToListAsync();

            var closingPeriods = closingPeriodRecords
                .GroupBy(cp => cp.Name!.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .OrderByDescending(cp => cp.PeriodEnd)
                        .First(),
                    StringComparer.OrdinalIgnoreCase);

            var engagements = await context.Engagements
                .AsNoTrackingWithIdentityResolution()
                .AsSplitQuery()
                .Include(e => e.Customer)
                .Include(e => e.EngagementPapds)
                    .ThenInclude(ep => ep.Papd)
                .Include(e => e.ManagerAssignments)
                    .ThenInclude(ma => ma.Manager)
                .Include(e => e.RankBudgets)
                    .ThenInclude(rb => rb.FiscalYear)
                .Include(e => e.RevenueAllocations)
                    .ThenInclude(a => a.FiscalYear)
                .Include(e => e.FinancialEvolutions)
                .Include(e => e.LastClosingPeriod)
                .ToListAsync();

            ApplyFinancialControlSnapshots(engagements, closingPeriods);

            return engagements;
        }

        public async Task<Engagement?> GetByIdAsync(int id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            ArgumentNullException.ThrowIfNull(context);

            var closingPeriodRecords = await context.ClosingPeriods
                .AsNoTracking()
                .Where(cp => !string.IsNullOrWhiteSpace(cp.Name))
                .ToListAsync();

            var closingPeriods = closingPeriodRecords
                .GroupBy(cp => cp.Name!.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .OrderByDescending(cp => cp.PeriodEnd)
                        .First(),
                    StringComparer.OrdinalIgnoreCase);

            var engagement = await context.Engagements
                .AsNoTrackingWithIdentityResolution()
                .AsSplitQuery()
                .Include(e => e.Customer)
                .Include(e => e.EngagementPapds)
                    .ThenInclude(ep => ep.Papd)
                .Include(e => e.ManagerAssignments)
                    .ThenInclude(ma => ma.Manager)
                .Include(e => e.RankBudgets)
                    .ThenInclude(rb => rb.FiscalYear)
                .Include(e => e.RevenueAllocations)
                    .ThenInclude(a => a.FiscalYear)
                .Include(e => e.FinancialEvolutions)
                .Include(e => e.LastClosingPeriod)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (engagement != null)
            {
                ApplyFinancialControlSnapshot(engagement, closingPeriods);
            }

            return engagement;
        }

        public async Task<Papd?> GetPapdForDateAsync(int engagementId, DateTime date)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            ArgumentNullException.ThrowIfNull(context);

            var assignment = await context.EngagementPapds
                .AsNoTracking()
                .Include(ep => ep.Papd)
                .Where(ep => ep.EngagementId == engagementId && ep.EffectiveDate <= date)
                .OrderByDescending(ep => ep.EffectiveDate)
                .FirstOrDefaultAsync();

            return assignment?.Papd;
        }

        private static void ApplyFinancialControlSnapshots(IEnumerable<Engagement> engagements, IReadOnlyDictionary<string, ClosingPeriod> closingPeriods)
        {
            foreach (var engagement in engagements)
            {
                ApplyFinancialControlSnapshot(engagement, closingPeriods);
            }
        }

        private static void ApplyFinancialControlSnapshot(Engagement engagement, IReadOnlyDictionary<string, ClosingPeriod> closingPeriods)
        {
            if (engagement.FinancialEvolutions == null || engagement.FinancialEvolutions.Count == 0)
            {
                engagement.InitialHoursBudget = 0m;
                engagement.OpeningValue = 0m;
                engagement.OpeningExpenses = 0m;
                engagement.MarginPctBudget = null;
                engagement.EstimatedToCompleteHours = 0m;
                engagement.ValueEtcp = 0m;
                engagement.ExpensesEtcp = 0m;
                engagement.MarginPctEtcp = null;
                engagement.LastClosingPeriodId = null;
                engagement.LastClosingPeriod = null;
                return;
            }

            var initialSnapshot = engagement.FinancialEvolutions
                .FirstOrDefault(evolution => string.Equals(
                    evolution.ClosingPeriodId,
                    FinancialEvolutionInitialPeriodId,
                    StringComparison.OrdinalIgnoreCase));

            if (initialSnapshot != null)
            {
                engagement.InitialHoursBudget = initialSnapshot.HoursData ?? 0m;
                engagement.OpeningValue = initialSnapshot.ValueData ?? 0m;
                engagement.OpeningExpenses = initialSnapshot.ExpenseData ?? 0m;
                engagement.MarginPctBudget = initialSnapshot.MarginData;
            }
            else
            {
                engagement.InitialHoursBudget = 0m;
                engagement.OpeningValue = 0m;
                engagement.OpeningExpenses = 0m;
                engagement.MarginPctBudget = null;
            }

            var latestSnapshot = engagement.FinancialEvolutions
                .Where(evolution => !string.IsNullOrWhiteSpace(evolution.ClosingPeriodId))
                .Where(evolution => !string.Equals(
                    evolution.ClosingPeriodId,
                    FinancialEvolutionInitialPeriodId,
                    StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(evolution => BuildFinancialEvolutionSortKey(evolution, closingPeriods))
                .ThenByDescending(evolution => evolution.Id)
                .FirstOrDefault();

            if (latestSnapshot != null)
            {
                engagement.EstimatedToCompleteHours = latestSnapshot.HoursData ?? 0m;
                engagement.ValueEtcp = latestSnapshot.ValueData ?? 0m;
                engagement.ExpensesEtcp = latestSnapshot.ExpenseData ?? 0m;
                engagement.MarginPctEtcp = latestSnapshot.MarginData;
                var normalizedClosingPeriodId = Normalize(latestSnapshot.ClosingPeriodId);

                if (!string.IsNullOrEmpty(normalizedClosingPeriodId) &&
                    closingPeriods.TryGetValue(normalizedClosingPeriodId, out var closingPeriod))
                {
                    engagement.LastClosingPeriodId = closingPeriod.Id;
                    engagement.LastClosingPeriod = closingPeriod;
                }
                else
                {
                    engagement.LastClosingPeriodId = null;
                    engagement.LastClosingPeriod = null;
                }
            }
            else
            {
                engagement.EstimatedToCompleteHours = 0m;
                engagement.ValueEtcp = 0m;
                engagement.ExpensesEtcp = 0m;
                engagement.MarginPctEtcp = null;
                engagement.LastClosingPeriodId = null;
                engagement.LastClosingPeriod = null;
            }
        }

        private static (int Priority, DateTime SortDate, int NumericValue, string NormalizedId) BuildFinancialEvolutionSortKey(
            FinancialEvolution evolution,
            IReadOnlyDictionary<string, ClosingPeriod> closingPeriods)
        {
            var closingPeriodId = Normalize(evolution.ClosingPeriodId);

            if (!string.IsNullOrEmpty(closingPeriodId) && closingPeriods.TryGetValue(closingPeriodId, out var closingPeriod))
            {
                return (3, closingPeriod.PeriodEnd, int.MaxValue, closingPeriodId);
            }

            if (!string.IsNullOrEmpty(closingPeriodId) && TryParsePeriodDate(closingPeriodId, out var parsedDate))
            {
                return (2, parsedDate, int.MaxValue, closingPeriodId);
            }

            if (!string.IsNullOrEmpty(closingPeriodId) && TryExtractDigits(closingPeriodId, out var numericValue))
            {
                return (1, DateTime.MinValue, numericValue, closingPeriodId);
            }

            return (0, DateTime.MinValue, int.MinValue, closingPeriodId ?? string.Empty);
        }

        public async Task AddAsync(Engagement engagement)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            await context.Engagements.AddAsync(engagement);
            await context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Engagement engagement)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var existingEngagement = await context.Engagements
                .Include(e => e.EngagementPapds)
                .Include(e => e.RevenueAllocations)
                    .ThenInclude(a => a.FiscalYear)
                .Include(e => e.FinancialEvolutions)
                .FirstOrDefaultAsync(e => e.Id == engagement.Id);

            if (existingEngagement == null)
            {
                throw new InvalidOperationException($"Engagement with Id={engagement.Id} could not be found.");
            }

            if (existingEngagement.Source == EngagementSource.S4Project)
            {
                throw new InvalidOperationException(
                    $"Engagement '{existingEngagement.EngagementId}' is sourced from S/4Project and must be managed manually. " +
                    "Updates through the application are blocked.");
            }

            static string FormatFiscalYearName(FiscalYear fiscalYear) => string.IsNullOrWhiteSpace(fiscalYear.Name)
                ? $"Id={fiscalYear.Id}"
                : fiscalYear.Name;

            var lockedRevenueAllocations = existingEngagement.RevenueAllocations
                .Where(a => a.FiscalYear?.IsLocked ?? false)
                .ToDictionary(a => a.FiscalYearId, a => a.FiscalYear!);
            var incomingRevenueAllocations = engagement.RevenueAllocations.ToDictionary(a => a.FiscalYearId, a => a);
            foreach (var (fiscalYearId, fiscalYear) in lockedRevenueAllocations)
            {
                if (!incomingRevenueAllocations.TryGetValue(fiscalYearId, out var incomingAllocation))
                {
                    throw new InvalidOperationException($"Cannot remove revenue allocation for locked fiscal year '{FormatFiscalYearName(fiscalYear)}'. Unlock it before making changes.");
                }

                var existingAllocation = existingEngagement.RevenueAllocations.First(a => a.FiscalYearId == fiscalYearId);

                var existingTotal = Math.Round(existingAllocation.ToGoValue + existingAllocation.ToDateValue, 2, MidpointRounding.AwayFromZero);
                var incomingTotal = Math.Round(incomingAllocation.ToGoValue + incomingAllocation.ToDateValue, 2, MidpointRounding.AwayFromZero);

                if (existingTotal != incomingTotal)
                {
                    throw new InvalidOperationException($"Cannot change revenue allocation for locked fiscal year '{FormatFiscalYearName(fiscalYear)}'. Unlock it before making changes.");
                }
            }

            var unlockedRevenueFiscalYearIds = engagement.RevenueAllocations
                .Select(a => a.FiscalYearId)
                .Where(id => !lockedRevenueAllocations.ContainsKey(id))
                .ToList();

            await FiscalYearLockGuard.EnsureFiscalYearsUnlockedAsync(context, unlockedRevenueFiscalYearIds, "update revenue allocations");

            context.Entry(existingEngagement).CurrentValues.SetValues(engagement);

            context.EngagementPapds.RemoveRange(existingEngagement.EngagementPapds);
            existingEngagement.EngagementPapds.Clear();

            foreach (var assignment in engagement.EngagementPapds.OrderBy(a => a.EffectiveDate))
            {
                var papdId = assignment.PapdId;
                if (papdId == 0 && assignment.Papd != null)
                {
                    papdId = assignment.Papd.Id;
                }

                if (papdId == 0)
                {
                    throw new InvalidOperationException("Cannot update engagement PAPD assignments without a valid PapdId.");
                }

                existingEngagement.EngagementPapds.Add(new EngagementPapd
                {
                    EngagementId = existingEngagement.Id,
                    PapdId = papdId,
                    EffectiveDate = assignment.EffectiveDate
                });
            }

            context.FinancialEvolutions.RemoveRange(existingEngagement.FinancialEvolutions);
            existingEngagement.FinancialEvolutions.Clear();

            var financialEvolutions = engagement.FinancialEvolutions
                .Where(e => !string.IsNullOrWhiteSpace(e.ClosingPeriodId))
                .GroupBy(e => e.ClosingPeriodId!, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(e => e.Id).First());

            foreach (var evolution in financialEvolutions)
            {
                var closingPeriodId = (evolution.ClosingPeriodId ?? string.Empty).Trim();
                existingEngagement.FinancialEvolutions.Add(new FinancialEvolution
                {
                    ClosingPeriodId = closingPeriodId,
                    EngagementId = existingEngagement.Id,
                    Engagement = existingEngagement,
                    HoursData = evolution.HoursData,
                    ValueData = evolution.ValueData,
                    MarginData = evolution.MarginData,
                    ExpenseData = evolution.ExpenseData
                });
            }

            var removableRevenueAllocations = existingEngagement.RevenueAllocations
                .Where(a => !lockedRevenueAllocations.ContainsKey(a.FiscalYearId))
                .ToList();

            context.EngagementFiscalYearRevenueAllocations.RemoveRange(removableRevenueAllocations);

            foreach (var allocation in removableRevenueAllocations)
            {
                existingEngagement.RevenueAllocations.Remove(allocation);
            }

            foreach (var allocation in engagement.RevenueAllocations.Where(a => !lockedRevenueAllocations.ContainsKey(a.FiscalYearId)))
            {
                existingEngagement.RevenueAllocations.Add(new EngagementFiscalYearRevenueAllocation
                {
                    FiscalYearId = allocation.FiscalYearId,
                    ToGoValue = allocation.ToGoValue,
                    ToDateValue = allocation.ToDateValue,
                    EngagementId = existingEngagement.Id
                });
            }

            await context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var engagement = await context.Engagements
                .Include(e => e.EngagementPapds)
                .FirstOrDefaultAsync(e => e.Id == id);
            if (engagement != null)
            {
                if (engagement.Source == EngagementSource.S4Project)
                {
                    throw new InvalidOperationException(
                        $"Engagement '{engagement.EngagementId}' is sourced from S/4Project and must be managed manually. " +
                        "Deletion is not permitted.");
                }

                context.Engagements.Remove(engagement);
                await context.SaveChangesAsync();
            }
        }

        public async Task DeleteDataAsync(int engagementId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var engagement = await context.Engagements
                .Include(e => e.EngagementPapds)
                .Include(e => e.RankBudgets)
                    .ThenInclude(rb => rb.FiscalYear)
                .Include(e => e.FinancialEvolutions)
                .Include(e => e.RevenueAllocations)
                    .ThenInclude(a => a.FiscalYear)
                .FirstOrDefaultAsync(e => e.Id == engagementId);

            if (engagement == null)
            {
                return;
            }

            if (engagement.Source == EngagementSource.S4Project)
            {
                throw new InvalidOperationException(
                    $"Engagement '{engagement.EngagementId}' is sourced from S/4Project and must be managed manually. " +
                    "Data deletion is not permitted.");
            }

            var lockedFiscalYears = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var budget in engagement.RankBudgets.Where(b => b.FiscalYear?.IsLocked ?? false))
            {
                lockedFiscalYears.Add(FormatFiscalYearName(budget.FiscalYear!, budget.FiscalYearId));
            }

            foreach (var allocation in engagement.RevenueAllocations.Where(a => a.FiscalYear?.IsLocked ?? false))
            {
                lockedFiscalYears.Add(FormatFiscalYearName(allocation.FiscalYear!, allocation.FiscalYearId));
            }

            var lockedActualFiscalYears = await context.ActualsEntries
                .Where(a => a.EngagementId == engagement.Id)
                .Join(context.ClosingPeriods.Include(cp => cp.FiscalYear),
                    a => a.ClosingPeriodId,
                    cp => cp.Id,
                    (a, cp) => cp.FiscalYear)
                .Where(fy => fy.IsLocked)
                .Select(fy => new { fy.Id, fy.Name })
                .Distinct()
                .ToListAsync();

            foreach (var fiscalYear in lockedActualFiscalYears)
            {
                var name = string.IsNullOrWhiteSpace(fiscalYear.Name) ? $"Id={fiscalYear.Id}" : fiscalYear.Name;
                lockedFiscalYears.Add(name);
            }

            if (lockedFiscalYears.Count > 0)
            {
                var formatted = string.Join(", ", lockedFiscalYears);
                throw new InvalidOperationException($"Cannot delete engagement data because the following fiscal year(s) are locked: {formatted}. Unlock them before retrying.");
            }

            var actualsToDelete = await context.ActualsEntries
                .Where(a => a.EngagementId == engagement.Id)
                .ToListAsync();
            context.ActualsEntries.RemoveRange(actualsToDelete);

            var plannedAllocationsToDelete = await context.PlannedAllocations
                .Where(p => p.EngagementId == engagement.Id)
                .ToListAsync();
            context.PlannedAllocations.RemoveRange(plannedAllocationsToDelete);

            context.EngagementPapds.RemoveRange(engagement.EngagementPapds);
            context.EngagementRankBudgets.RemoveRange(engagement.RankBudgets);
            context.FinancialEvolutions.RemoveRange(engagement.FinancialEvolutions);
            context.EngagementFiscalYearRevenueAllocations.RemoveRange(engagement.RevenueAllocations);

            await context.SaveChangesAsync();
        }

        private static string FormatFiscalYearName(FiscalYear fiscalYear, int fiscalYearId)
        {
            return string.IsNullOrWhiteSpace(fiscalYear?.Name)
                ? $"Id={fiscalYearId}"
                : fiscalYear!.Name;
        }
    }
}