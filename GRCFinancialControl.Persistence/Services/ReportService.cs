using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Core.Models.Reporting;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using static GRCFinancialControl.Persistence.Services.ClosingPeriodIdHelper;

namespace GRCFinancialControl.Persistence.Services
{
    public class ReportService : IReportService
    {
        private const string FinancialEvolutionInitialPeriodId = "INITIAL";

        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public ReportService(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<List<PapdContributionData>> GetPapdContributionDataAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var papds = await context.Papds
                .AsNoTracking()
                .Select(p => new { p.Id, p.Name })
                .ToListAsync();

            var assignments = await context.EngagementPapds
                .AsNoTracking()
                .Join(
                    context.Engagements.AsNoTracking(),
                    ep => ep.EngagementId,
                    e => e.Id,
                    (ep, engagement) => new { ep.PapdId, EngagementId = engagement.Id })
                .ToListAsync();

            var papdToEngagementIds = assignments
                .GroupBy(a => a.PapdId)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .Select(a => a.EngagementId)
                        .Where(id => id > 0)
                        .Distinct()
                        .ToList());

            var relevantEngagementIds = papdToEngagementIds
                .SelectMany(kvp => kvp.Value)
                .Distinct()
                .ToList();

            var closingPeriodRecords = await context.ClosingPeriods
                .AsNoTracking()
                .Where(cp => !string.IsNullOrWhiteSpace(cp.Name))
                .ToListAsync();

            var closingPeriods = closingPeriodRecords
                .Select(cp => new
                {
                    NormalizedName = cp.Name!.Trim(),
                    cp.PeriodEnd
                })
                .GroupBy(entry => entry.NormalizedName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .OrderByDescending(entry => entry.PeriodEnd)
                        .First()
                        .PeriodEnd,
                    StringComparer.OrdinalIgnoreCase);

            var sortKeyCache = new Dictionary<string, (int Priority, DateTime SortDate, int NumericValue, string NormalizedId)>(StringComparer.OrdinalIgnoreCase);

            (int Priority, DateTime SortDate, int NumericValue, string NormalizedId) GetSortKey(string? closingPeriodId)
            {
                var normalized = Normalize(closingPeriodId) ?? string.Empty;
                if (!sortKeyCache.TryGetValue(normalized, out var key))
                {
                    key = BuildFinancialEvolutionSortKey(closingPeriodId, closingPeriods);
                    sortKeyCache[normalized] = key;
                }

                return key;
            }

            var evolutions = relevantEngagementIds.Count == 0
                ? new List<FinancialEvolution>()
                : await context.FinancialEvolutions
                    .AsNoTracking()
                    .Where(fe => relevantEngagementIds.Contains(fe.EngagementId))
                    .ToListAsync();

            var evolutionsByEngagement = evolutions
                .GroupBy(fe => fe.EngagementId)
                .ToDictionary(
                    group => group.Key,
                    group => group.ToList());

            var result = new List<PapdContributionData>(papds.Count);

            foreach (var papd in papds)
            {
                papdToEngagementIds.TryGetValue(papd.Id, out var engagementIds);

                var revenueContribution = 0m;
                var hoursByPeriod = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

                if (engagementIds != null)
                {
                    foreach (var engagementId in engagementIds)
                    {
                        if (engagementId <= 0 ||
                            !evolutionsByEngagement.TryGetValue(engagementId, out var snapshots))
                        {
                            continue;
                        }

                        var latestSnapshot = snapshots
                            .Where(snapshot => !string.Equals(
                                snapshot.ClosingPeriodId,
                                FinancialEvolutionInitialPeriodId,
                                StringComparison.OrdinalIgnoreCase))
                            .OrderByDescending(snapshot => GetSortKey(snapshot.ClosingPeriodId))
                            .ThenByDescending(snapshot => snapshot.Id)
                            .FirstOrDefault();

                        if (latestSnapshot?.ValueData is decimal latestValue)
                        {
                            revenueContribution += latestValue;
                        }
                        else
                        {
                            var initialSnapshot = snapshots.FirstOrDefault(snapshot => string.Equals(
                                snapshot.ClosingPeriodId,
                                FinancialEvolutionInitialPeriodId,
                                StringComparison.OrdinalIgnoreCase));

                            if (initialSnapshot?.ValueData is decimal initialValue)
                            {
                                revenueContribution += initialValue;
                            }
                        }

                        foreach (var snapshot in snapshots)
                        {
                            if (string.Equals(
                                    snapshot.ClosingPeriodId,
                                    FinancialEvolutionInitialPeriodId,
                                    StringComparison.OrdinalIgnoreCase) ||
                                !snapshot.HoursData.HasValue)
                            {
                                continue;
                            }

                            var periodId = Normalize(snapshot.ClosingPeriodId);
                            if (string.IsNullOrEmpty(periodId))
                            {
                                continue;
                            }

                            hoursByPeriod[periodId] = hoursByPeriod.TryGetValue(periodId, out var existing)
                                ? existing + snapshot.HoursData.Value
                                : snapshot.HoursData.Value;
                        }
                    }
                }

                var orderedHours = hoursByPeriod
                    .OrderBy(kvp => GetSortKey(kvp.Key))
                    .Select(kvp => new HoursWorked
                    {
                        ClosingPeriodName = kvp.Key,
                        Hours = kvp.Value
                    })
                    .ToList();

                var contribution = new PapdContributionData
                {
                    PapdName = papd.Name,
                    RevenueContribution = revenueContribution
                };

                foreach (var hoursWorked in orderedHours)
                {
                    contribution.HoursWorked.Add(hoursWorked);
                }

                result.Add(contribution);
            }

            return result;
        }

        public async Task<List<FinancialEvolutionPoint>> GetFinancialEvolutionPointsAsync(string engagementId)
        {
            if (string.IsNullOrWhiteSpace(engagementId))
            {
                return new List<FinancialEvolutionPoint>();
            }

            var normalizedKey = engagementId.Trim();

            await using var context = await _contextFactory.CreateDbContextAsync();

            var engagementDbId = await context.Engagements
                .AsNoTracking()
                .Where(e => e.EngagementId == normalizedKey)
                .Select(e => (int?)e.Id)
                .FirstOrDefaultAsync();

            if (!engagementDbId.HasValue)
            {
                return new List<FinancialEvolutionPoint>();
            }

            var entries = await context.FinancialEvolutions
                .AsNoTracking()
                .Where(fe => fe.EngagementId == engagementDbId.Value)
                .Where(HasRelevantMetricsExpression())
                .GroupJoin(
                    context.ClosingPeriods.AsNoTracking(),
                    fe => fe.ClosingPeriodId,
                    cp => cp.Name,
                    (fe, cps) => new { FinancialEvolution = fe, ClosingPeriods = cps })
                .SelectMany(
                    x => x.ClosingPeriods.DefaultIfEmpty(),
                    (x, cp) => new
                    {
                        x.FinancialEvolution.ClosingPeriodId,
                        PeriodEnd = cp != null ? (DateTime?)cp.PeriodEnd : null,
                        x.FinancialEvolution.HoursData,
                        x.FinancialEvolution.ValueData,
                        x.FinancialEvolution.MarginData,
                        x.FinancialEvolution.ExpenseData
                    })
                .ToListAsync();

            var points = entries
                .Select(fe => new FinancialEvolutionPoint
                {
                    ClosingPeriodId = fe.ClosingPeriodId,
                    ClosingPeriodDate = fe.PeriodEnd,
                    Hours = fe.HoursData,
                    Revenue = fe.ValueData,
                    Margin = fe.MarginData,
                    Expenses = fe.ExpenseData
                })
                .OrderBy(p => GetClosingPeriodSortKey(p.ClosingPeriodId))
                .ThenBy(p => p.ClosingPeriodId, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return points;
        }

        private static Expression<Func<FinancialEvolution, bool>> HasRelevantMetricsExpression()
        {
            return evolution => (evolution.HoursData ?? 0m) != 0m
                                 || (evolution.ValueData ?? 0m) != 0m
                                 || (evolution.MarginData ?? 0m) != 0m
                                 || (evolution.ExpenseData ?? 0m) != 0m;
        }

        private static (int Group, int Number) GetClosingPeriodSortKey(string closingPeriodId)
        {
            if (string.Equals(closingPeriodId, "INITIAL", StringComparison.OrdinalIgnoreCase))
            {
                return (0, 0);
            }

            if (closingPeriodId.StartsWith("CP", StringComparison.OrdinalIgnoreCase)
                && closingPeriodId.Length > 2
                && int.TryParse(closingPeriodId.Substring(2), out var periodNumber))
            {
                return (1, periodNumber);
            }

            return (2, 0);
        }

        private static (int Priority, DateTime SortDate, int NumericValue, string NormalizedId) BuildFinancialEvolutionSortKey(
            string? closingPeriodId,
            IReadOnlyDictionary<string, DateTime> closingPeriods)
        {
            var normalizedId = Normalize(closingPeriodId);

            if (!string.IsNullOrEmpty(normalizedId) && closingPeriods.TryGetValue(normalizedId, out var periodEnd))
            {
                return (3, periodEnd, int.MaxValue, normalizedId);
            }

            if (!string.IsNullOrEmpty(normalizedId) && TryParsePeriodDate(normalizedId, out var parsedDate))
            {
                return (2, parsedDate, int.MaxValue, normalizedId);
            }

            if (!string.IsNullOrEmpty(normalizedId) && TryExtractDigits(normalizedId, out var numericValue))
            {
                return (1, DateTime.MinValue, numericValue, normalizedId);
            }

            return (0, DateTime.MinValue, int.MinValue, normalizedId ?? string.Empty);
        }

    }
}
