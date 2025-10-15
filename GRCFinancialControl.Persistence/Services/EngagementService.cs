using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

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
                .GroupBy(cp => cp.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .OrderByDescending(cp => cp.PeriodEnd)
                        .First()
                        .PeriodEnd,
                    StringComparer.OrdinalIgnoreCase);

            var engagements = await context.Engagements
                .AsNoTrackingWithIdentityResolution()
                .AsSplitQuery()
                .Include(e => e.Customer)
                .Include(e => e.EngagementPapds)
                    .ThenInclude(ep => ep.Papd)
                .Include(e => e.ManagerAssignments)
                    .ThenInclude(ma => ma.Manager)
                .Include(e => e.Allocations)
                    .ThenInclude(a => a.FiscalYear)
                .Include(e => e.RevenueAllocations)
                    .ThenInclude(a => a.FiscalYear)
                .Include(e => e.FinancialEvolutions)
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
                .GroupBy(cp => cp.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .OrderByDescending(cp => cp.PeriodEnd)
                        .First()
                        .PeriodEnd,
                    StringComparer.OrdinalIgnoreCase);

            var engagement = await context.Engagements
                .AsNoTrackingWithIdentityResolution()
                .AsSplitQuery()
                .Include(e => e.Customer)
                .Include(e => e.EngagementPapds)
                    .ThenInclude(ep => ep.Papd)
                .Include(e => e.ManagerAssignments)
                    .ThenInclude(ma => ma.Manager)
                .Include(e => e.Allocations)
                    .ThenInclude(a => a.FiscalYear)
                .Include(e => e.RevenueAllocations)
                    .ThenInclude(a => a.FiscalYear)
                .Include(e => e.FinancialEvolutions)
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

        private static void ApplyFinancialControlSnapshots(IEnumerable<Engagement> engagements, IReadOnlyDictionary<string, DateTime> closingPeriods)
        {
            foreach (var engagement in engagements)
            {
                ApplyFinancialControlSnapshot(engagement, closingPeriods);
            }
        }

        private static void ApplyFinancialControlSnapshot(Engagement engagement, IReadOnlyDictionary<string, DateTime> closingPeriods)
        {
            if (engagement.FinancialEvolutions == null || engagement.FinancialEvolutions.Count == 0)
            {
                engagement.InitialHoursBudget = 0m;
                engagement.OpeningValue = 0m;
                engagement.OpeningExpenses = 0m;
                engagement.MarginPctBudget = null;
                engagement.EtcpHours = 0m;
                engagement.ValueEtcp = 0m;
                engagement.ExpensesEtcp = 0m;
                engagement.MarginPctEtcp = null;
                engagement.LastClosingPeriodId = null;
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
                engagement.EtcpHours = latestSnapshot.HoursData ?? 0m;
                engagement.ValueEtcp = latestSnapshot.ValueData ?? 0m;
                engagement.ExpensesEtcp = latestSnapshot.ExpenseData ?? 0m;
                engagement.MarginPctEtcp = latestSnapshot.MarginData;
                engagement.LastClosingPeriodId = NormalizeClosingPeriodId(latestSnapshot.ClosingPeriodId);
            }
            else
            {
                engagement.EtcpHours = 0m;
                engagement.ValueEtcp = 0m;
                engagement.ExpensesEtcp = 0m;
                engagement.MarginPctEtcp = null;
                engagement.LastClosingPeriodId = null;
            }
        }

        private static (int Priority, DateTime SortDate, int NumericValue, string NormalizedId) BuildFinancialEvolutionSortKey(
            FinancialEvolution evolution,
            IReadOnlyDictionary<string, DateTime> closingPeriods)
        {
            var closingPeriodId = NormalizeClosingPeriodId(evolution.ClosingPeriodId);

            if (!string.IsNullOrEmpty(closingPeriodId) && closingPeriods.TryGetValue(closingPeriodId, out var periodEnd))
            {
                return (3, periodEnd, int.MaxValue, closingPeriodId);
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

        private static string? NormalizeClosingPeriodId(string? closingPeriodId)
        {
            if (string.IsNullOrWhiteSpace(closingPeriodId))
            {
                return null;
            }

            return closingPeriodId.Trim();
        }

        private static bool TryParsePeriodDate(string closingPeriodId, out DateTime parsedDate)
        {
            return DateTime.TryParse(
                closingPeriodId,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out parsedDate);
        }

        private static bool TryExtractDigits(string closingPeriodId, out int numericValue)
        {
            var digits = closingPeriodId.Where(char.IsDigit).ToArray();
            if (digits.Length == 0)
            {
                numericValue = 0;
                return false;
            }

            return int.TryParse(new string(digits), NumberStyles.Integer, CultureInfo.InvariantCulture, out numericValue);
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
                .Include(e => e.Allocations)
                .Include(e => e.RevenueAllocations)
                .Include(e => e.FinancialEvolutions)
                .FirstOrDefaultAsync(e => e.Id == engagement.Id);

            if (existingEngagement != null)
            {
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
                        EngagementId = existingEngagement.EngagementId,
                        HoursData = evolution.HoursData,
                        ValueData = evolution.ValueData,
                        MarginData = evolution.MarginData,
                        ExpenseData = evolution.ExpenseData
                    });
                }

                context.EngagementFiscalYearAllocations.RemoveRange(existingEngagement.Allocations);

                foreach (var allocation in engagement.Allocations)
                {
                    existingEngagement.Allocations.Add(new EngagementFiscalYearAllocation
                    {
                        FiscalYearId = allocation.FiscalYearId,
                        PlannedHours = allocation.PlannedHours,
                        EngagementId = existingEngagement.Id
                    });
                }

                context.EngagementFiscalYearRevenueAllocations.RemoveRange(existingEngagement.RevenueAllocations);

                foreach (var allocation in engagement.RevenueAllocations)
                {
                    existingEngagement.RevenueAllocations.Add(new EngagementFiscalYearRevenueAllocation
                    {
                        FiscalYearId = allocation.FiscalYearId,
                        PlannedValue = allocation.PlannedValue,
                        EngagementId = existingEngagement.Id
                    });
                }

                await context.SaveChangesAsync();
            }
        }

        public async Task DeleteAsync(int id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var engagement = await context.Engagements
                .Include(e => e.EngagementPapds)
                .FirstOrDefaultAsync(e => e.Id == id);
            if (engagement != null)
            {
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
                .Include(e => e.FinancialEvolutions)
                .Include(e => e.Allocations)
                .Include(e => e.RevenueAllocations)
                .FirstOrDefaultAsync(e => e.Id == engagementId);

            if (engagement == null) return;

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
            context.EngagementFiscalYearAllocations.RemoveRange(engagement.Allocations);
            context.EngagementFiscalYearRevenueAllocations.RemoveRange(engagement.RevenueAllocations);

            await context.SaveChangesAsync();
        }
    }
}