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
                    (ep, engagement) => new { ep.PapdId, engagement.EngagementId })
                .Where(x => !string.IsNullOrWhiteSpace(x.EngagementId))
                .ToListAsync();

            var papdToEngagementKeys = assignments
                .GroupBy(a => a.PapdId)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .Select(a => a.EngagementId!)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList());

            var relevantEngagementKeys = papdToEngagementKeys
                .SelectMany(kvp => kvp.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var closingPeriods = await context.ClosingPeriods
                .AsNoTracking()
                .Where(cp => !string.IsNullOrWhiteSpace(cp.Name))
                .GroupBy(cp => cp.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionaryAsync(
                    group => group.Key,
                    group => group
                        .OrderByDescending(cp => cp.PeriodEnd)
                        .First()
                        .PeriodEnd,
                    StringComparer.OrdinalIgnoreCase);

            var evolutions = relevantEngagementKeys.Count == 0
                ? new List<FinancialEvolution>()
                : await context.FinancialEvolutions
                    .AsNoTracking()
                    .Where(fe => relevantEngagementKeys.Contains(fe.EngagementId))
                    .ToListAsync();

            var evolutionsByEngagement = evolutions
                .GroupBy(fe => fe.EngagementId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.ToList(),
                    StringComparer.OrdinalIgnoreCase);

            var result = new List<PapdContributionData>(papds.Count);

            foreach (var papd in papds)
            {
                papdToEngagementKeys.TryGetValue(papd.Id, out var engagementKeys);

                var revenueContribution = 0m;
                var hoursByPeriod = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

                if (engagementKeys != null)
                {
                    foreach (var engagementKey in engagementKeys)
                    {
                        if (string.IsNullOrWhiteSpace(engagementKey) ||
                            !evolutionsByEngagement.TryGetValue(engagementKey, out var snapshots))
                        {
                            continue;
                        }

                        var latestSnapshot = snapshots
                            .Where(snapshot => !string.Equals(
                                snapshot.ClosingPeriodId,
                                FinancialEvolutionInitialPeriodId,
                                StringComparison.OrdinalIgnoreCase))
                            .OrderByDescending(snapshot => BuildFinancialEvolutionSortKey(snapshot.ClosingPeriodId, closingPeriods))
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

                            var periodId = NormalizeClosingPeriodId(snapshot.ClosingPeriodId);
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
                    .OrderBy(kvp => BuildFinancialEvolutionSortKey(kvp.Key, closingPeriods))
                    .Select(kvp => new HoursWorked
                    {
                        ClosingPeriodName = kvp.Key,
                        Hours = kvp.Value
                    })
                    .ToList();

                result.Add(new PapdContributionData
                {
                    PapdName = papd.Name,
                    RevenueContribution = revenueContribution,
                    HoursWorked = orderedHours
                });
            }

            return result;
        }

        public async Task<List<FinancialEvolutionPoint>> GetFinancialEvolutionPointsAsync(string engagementId)
        {
            if (string.IsNullOrWhiteSpace(engagementId))
            {
                return new List<FinancialEvolutionPoint>();
            }

            await using var context = await _contextFactory.CreateDbContextAsync();

            var entries = await context.FinancialEvolutions
                .AsNoTracking()
                .Where(fe => fe.EngagementId == engagementId)
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
            var normalizedId = NormalizeClosingPeriodId(closingPeriodId);

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

        private static string? NormalizeClosingPeriodId(string? closingPeriodId)
            => string.IsNullOrWhiteSpace(closingPeriodId) ? null : closingPeriodId.Trim();

        private static bool TryParsePeriodDate(string closingPeriodId, out DateTime parsedDate)
        {
            return DateTime.TryParse(
                closingPeriodId,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out parsedDate);
        }

        private static bool TryExtractDigits(string closingPeriodId, out int numericValue)
        {
            numericValue = 0;
            var digits = new string(closingPeriodId.Where(char.IsDigit).ToArray());
            return digits.Length > 0 && int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out numericValue);
        }
    }
}
