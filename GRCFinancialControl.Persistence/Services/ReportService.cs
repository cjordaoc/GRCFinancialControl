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
                .ToListAsync();

            var closingPeriodLookup = BuildClosingPeriodLookup(closingPeriodRecords);

            var sortKeyCache = new Dictionary<string, (int Priority, DateTime SortDate, int NumericValue, string NormalizedId)>(StringComparer.OrdinalIgnoreCase);

            (int Priority, DateTime SortDate, int NumericValue, string NormalizedId) GetSortKey(string? closingPeriodId)
            {
                var normalized = Normalize(closingPeriodId) ?? string.Empty;
                if (!sortKeyCache.TryGetValue(normalized, out var key))
                {
                    key = BuildFinancialEvolutionSortKey(closingPeriodId, closingPeriodLookup);
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
                var hoursByPeriod = new Dictionary<string, (decimal Hours, string DisplayName)>(StringComparer.OrdinalIgnoreCase);

                if (engagementIds != null)
                {
                    foreach (var engagementId in engagementIds)
                    {
                        if (engagementId <= 0 ||
                            !evolutionsByEngagement.TryGetValue(engagementId, out var snapshots))
                        {
                            continue;
                        }

                        var orderedSnapshots = OrderSnapshots(snapshots);

                        if (orderedSnapshots.Count > 0)
                        {
                            var latestSnapshot = orderedSnapshots.Last().Snapshot;
                            if (latestSnapshot.ValueData is decimal latestValue)
                            {
                                revenueContribution += latestValue;
                            }
                            else
                            {
                                var baselineSnapshot = orderedSnapshots.First().Snapshot;
                                if (baselineSnapshot.ValueData is decimal baselineValue)
                                {
                                    revenueContribution += baselineValue;
                                }
                            }

                            foreach (var candidate in orderedSnapshots)
                            {
                                var snapshot = candidate.Snapshot;
                                if (!snapshot.ChargedHours.HasValue)
                                {
                                    continue;
                                }

                                var normalizedId = Normalize(snapshot.ClosingPeriodId);
                                if (string.IsNullOrEmpty(normalizedId))
                                {
                                    continue;
                                }

                                string bucketKey = normalizedId;
                                string displayName = normalizedId;

                                if (closingPeriodLookup.TryGetValue(normalizedId, out var closingPeriod))
                                {
                                    bucketKey = closingPeriod.Id.ToString(CultureInfo.InvariantCulture);
                                    displayName = !string.IsNullOrWhiteSpace(closingPeriod.Name)
                                        ? closingPeriod.Name
                                        : closingPeriod.Id.ToString(CultureInfo.InvariantCulture);
                                }

                                if (hoursByPeriod.TryGetValue(bucketKey, out var existing))
                                {
                                    var resolvedDisplayName = string.IsNullOrWhiteSpace(existing.DisplayName)
                                        ? displayName
                                        : existing.DisplayName;
                                    hoursByPeriod[bucketKey] = (existing.Hours + snapshot.ChargedHours.Value, resolvedDisplayName);
                                }
                                else
                                {
                                    hoursByPeriod[bucketKey] = (snapshot.ChargedHours.Value, displayName);
                                }
                            }
                        }
                    }
                }

                var orderedHours = hoursByPeriod
                    .Select(kvp => new
                    {
                        Key = kvp.Key,
                        kvp.Value.DisplayName,
                        kvp.Value.Hours,
                        SortKey = GetSortKey(kvp.Key)
                    })
                    .OrderBy(entry => entry.SortKey.Priority)
                    .ThenBy(entry => entry.SortKey.SortDate)
                    .ThenBy(entry => entry.SortKey.NumericValue)
                    .Select(entry => new HoursWorked
                    {
                        ClosingPeriodName = entry.DisplayName,
                        Hours = entry.Hours
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

            List<(FinancialEvolution Snapshot, (int Priority, DateTime SortDate, int NumericValue, string NormalizedId) SortKey)> OrderSnapshots(List<FinancialEvolution> snapshots)
            {
                var candidates = snapshots
                    .Where(snapshot => snapshot is not null && !string.IsNullOrWhiteSpace(snapshot.ClosingPeriodId))
                    .Select(snapshot => (Snapshot: snapshot, SortKey: GetSortKey(snapshot.ClosingPeriodId)))
                    .ToList();

                if (candidates.Count == 0)
                {
                    return candidates;
                }

                var resolvedCandidates = candidates
                    .Where(candidate => !string.IsNullOrEmpty(candidate.SortKey.NormalizedId) && closingPeriodLookup.ContainsKey(candidate.SortKey.NormalizedId))
                    .ToList();

                return (resolvedCandidates.Count > 0 ? resolvedCandidates : candidates)
                    .OrderBy(candidate => candidate.SortKey.Priority)
                    .ThenBy(candidate => candidate.SortKey.SortDate)
                    .ThenBy(candidate => candidate.SortKey.NumericValue)
                    .ThenBy(candidate => candidate.Snapshot.Id)
                    .ToList();
            }
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

            var closingPeriodRecords = await context.ClosingPeriods
                .AsNoTracking()
                .ToListAsync();

            var closingPeriodLookup = BuildClosingPeriodLookup(closingPeriodRecords);

            var entries = await context.FinancialEvolutions
                .AsNoTracking()
                .Where(fe => fe.EngagementId == engagementDbId.Value)
                .Where(HasRelevantMetricsExpression())
                .ToListAsync();

            var points = entries
                .Select(evolution => new
                {
                    Evolution = evolution,
                    SortKey = BuildFinancialEvolutionSortKey(evolution.ClosingPeriodId, closingPeriodLookup),
                    PeriodEnd = ResolveClosingPeriodEnd(evolution.ClosingPeriodId, closingPeriodLookup)
                })
                .OrderBy(entry => entry.SortKey.Priority)
                .ThenBy(entry => entry.SortKey.SortDate)
                .ThenBy(entry => entry.SortKey.NumericValue)
                .ThenBy(entry => entry.Evolution.Id)
                .Select(entry => new FinancialEvolutionPoint
                {
                    ClosingPeriodId = entry.Evolution.ClosingPeriodId,
                    ClosingPeriodDate = entry.PeriodEnd,
                    Hours = entry.Evolution.ChargedHours,
                    Revenue = entry.Evolution.ValueData,
                    Margin = entry.Evolution.ToDateMargin,
                    Expenses = entry.Evolution.ExpensesToDate
                })
                .ToList();

            return points;

            static DateTime? ResolveClosingPeriodEnd(string? closingPeriodId, IReadOnlyDictionary<string, ClosingPeriod> lookup)
            {
                var normalized = Normalize(closingPeriodId);
                if (string.IsNullOrEmpty(normalized))
                {
                    return null;
                }

                return lookup.TryGetValue(normalized, out var closingPeriod)
                    ? closingPeriod.PeriodEnd
                    : null;
            }
        }

        private static Expression<Func<FinancialEvolution, bool>> HasRelevantMetricsExpression()
        {
            return evolution => (evolution.ChargedHours ?? 0m) != 0m
                                 || (evolution.ValueData ?? 0m) != 0m
                                 || (evolution.ToDateMargin ?? 0m) != 0m
                                 || (evolution.ExpensesToDate ?? 0m) != 0m;
        }

        private static IReadOnlyDictionary<string, ClosingPeriod> BuildClosingPeriodLookup(IEnumerable<ClosingPeriod> records)
        {
            ArgumentNullException.ThrowIfNull(records);

            var lookup = new Dictionary<string, ClosingPeriod>(StringComparer.OrdinalIgnoreCase);

            foreach (var group in records
                .Where(record => record is not null && !string.IsNullOrWhiteSpace(record.Name))
                .GroupBy(record => record.Name!.Trim(), StringComparer.OrdinalIgnoreCase))
            {
                var latest = group
                    .OrderByDescending(cp => cp.PeriodEnd)
                    .First();

                lookup[group.Key] = latest;
            }

            foreach (var period in records)
            {
                lookup[period.Id.ToString(CultureInfo.InvariantCulture)] = period;
            }

            return lookup;
        }

        private static (int Priority, DateTime SortDate, int NumericValue, string NormalizedId) BuildFinancialEvolutionSortKey(
            string? closingPeriodId,
            IReadOnlyDictionary<string, ClosingPeriod> closingPeriods)
        {
            var normalizedId = Normalize(closingPeriodId);

            if (!string.IsNullOrEmpty(normalizedId) && closingPeriods.TryGetValue(normalizedId, out var closingPeriod))
            {
                return (3, closingPeriod.PeriodEnd, closingPeriod.Id, normalizedId);
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
