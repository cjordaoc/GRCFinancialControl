using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Importers.StaffAllocations;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GRCFinancialControl.Persistence.Services
{
    public class StaffAllocationForecastService : IStaffAllocationForecastService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ILogger<StaffAllocationForecastService> _logger;

        public StaffAllocationForecastService(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            ILogger<StaffAllocationForecastService> logger)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<StaffAllocationForecastUpdateResult> UpdateForecastAsync(IReadOnlyList<StaffAllocationTemporaryRecord> records)
        {
            if (records == null)
            {
                throw new ArgumentNullException(nameof(records));
            }

            if (records.Count == 0)
            {
                return new StaffAllocationForecastUpdateResult(
                    ProcessedRecords: 0,
                    UpdatedEngagements: 0,
                    MissingEngagements: Array.Empty<string>(),
                    MissingBudgets: Array.Empty<string>(),
                    UnknownRanks: Array.Empty<string>(),
                    Rows: Array.Empty<ForecastAllocationRow>(),
                    RiskCount: 0,
                    OverrunCount: 0);
            }

            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);

            var normalizedRecords = records
                .Where(r => !string.IsNullOrWhiteSpace(r.EngagementCode) && r.FiscalYearId.HasValue)
                .Select(r => new ForecastAggregate(
                    r.EngagementCode.Trim(),
                    NormalizeRank(r.Rank),
                    r.FiscalYearId!.Value,
                    r.Hours))
                .ToList();

            if (normalizedRecords.Count == 0)
            {
                return new StaffAllocationForecastUpdateResult(
                    ProcessedRecords: records.Count,
                    UpdatedEngagements: 0,
                    MissingEngagements: Array.Empty<string>(),
                    MissingBudgets: Array.Empty<string>(),
                    UnknownRanks: Array.Empty<string>(),
                    Rows: Array.Empty<ForecastAllocationRow>(),
                    RiskCount: 0,
                    OverrunCount: 0);
            }

            var engagementCodes = normalizedRecords
                .Select(r => r.EngagementCode)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var engagementLookup = await context.Engagements
                .Include(e => e.RankBudgets)
                .Where(e => engagementCodes.Contains(e.EngagementId))
                .ToDictionaryAsync(e => e.EngagementId, StringComparer.OrdinalIgnoreCase)
                .ConfigureAwait(false);

            var engagementById = engagementLookup
                .Values
                .ToDictionary(e => e.Id);

            var missingEngagements = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var mappedAggregates = new List<ForecastAggregateResolved>();
            var engagementIds = new HashSet<int>();
            var unknownRanks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var aggregate in normalizedRecords)
            {
                if (!engagementLookup.TryGetValue(aggregate.EngagementCode, out var engagement))
                {
                    missingEngagements.Add(aggregate.EngagementCode);
                    continue;
                }

                var rank = NormalizeRank(aggregate.Rank);

                if (string.Equals(rank, "Unspecified", StringComparison.OrdinalIgnoreCase))
                {
                    unknownRanks.Add(rank);
                }

                mappedAggregates.Add(new ForecastAggregateResolved(
                    engagement.Id,
                    engagement.EngagementId,
                    engagement.Description,
                    aggregate.FiscalYearId,
                    rank,
                    aggregate.Hours));

                engagementIds.Add(engagement.Id);
            }

            if (mappedAggregates.Count == 0)
            {
                return new StaffAllocationForecastUpdateResult(
                    ProcessedRecords: records.Count,
                    UpdatedEngagements: 0,
                    MissingEngagements: missingEngagements.ToList(),
                    MissingBudgets: Array.Empty<string>(),
                    UnknownRanks: unknownRanks.ToList(),
                    Rows: Array.Empty<ForecastAllocationRow>(),
                    RiskCount: 0,
                    OverrunCount: 0);
            }

            var fiscalYears = await context.FiscalYears
                .AsNoTracking()
                .ToDictionaryAsync(fy => fy.Id)
                .ConfigureAwait(false);

            var aggregatesByKey = mappedAggregates
                .GroupBy(
                    a => new ForecastAggregateKey(a.EngagementId, a.FiscalYearId, a.Rank),
                    ForecastAggregateKeyComparer.Instance)
                .Select(g =>
                {
                    var first = g.First();
                    return first with { Hours = g.Sum(x => x.Hours) };
                })
                .ToList();

            var totalsByEngagementRank = aggregatesByKey
                .GroupBy(
                    a => new EngagementRankKey(a.EngagementId, a.Rank),
                    EngagementRankKeyComparer.Instance)
                .ToDictionary(
                    g => new EngagementRankKey(g.Key.EngagementId, g.Key.Rank),
                    g => g.Sum(x => x.Hours),
                    EngagementRankKeyComparer.Instance);

            var rankBudgets = await context.EngagementRankBudgets
                .Where(rb => engagementIds.Contains(rb.EngagementId))
                .ToListAsync()
                .ConfigureAwait(false);

            var budgetLookup = rankBudgets
                .ToDictionary(rb => new EngagementRankKey(rb.EngagementId, rb.RankName), rb => rb, EngagementRankKeyComparer.Instance);

            var missingBudgets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var now = DateTime.UtcNow;
            var processedBudgetKeys = new HashSet<EngagementRankKey>(EngagementRankKeyComparer.Instance);

            foreach (var (key, total) in totalsByEngagementRank)
            {
                if (budgetLookup.TryGetValue(key, out var budget))
                {
                    budget.ForecastHours = total;
                    budget.UpdatedAtUtc = now;
                    processedBudgetKeys.Add(new EngagementRankKey(budget.EngagementId, budget.RankName));
                }
                else
                {
                    var engagementName = engagementById.TryGetValue(key.EngagementId, out var engagement)
                        ? engagement.EngagementId
                        : key.EngagementId.ToString();
                    missingBudgets.Add($"{engagementName}:{key.Rank}");
                }
            }

            foreach (var budget in rankBudgets)
            {
                var budgetKey = new EngagementRankKey(budget.EngagementId, budget.RankName);
                if (engagementIds.Contains(budget.EngagementId) &&
                    !processedBudgetKeys.Contains(budgetKey))
                {
                    budget.ForecastHours = 0m;
                    budget.UpdatedAtUtc = now;
                }
            }

            if (engagementIds.Count > 0)
            {
                var existingForecasts = await context.StaffAllocationForecasts
                    .Where(f => engagementIds.Contains(f.EngagementId))
                    .ToListAsync()
                    .ConfigureAwait(false);

                if (existingForecasts.Count > 0)
                {
                    context.StaffAllocationForecasts.RemoveRange(existingForecasts);
                }
            }

            foreach (var aggregate in aggregatesByKey)
            {
                context.StaffAllocationForecasts.Add(new StaffAllocationForecast
                {
                    EngagementId = aggregate.EngagementId,
                    FiscalYearId = aggregate.FiscalYearId,
                    RankName = aggregate.Rank,
                    ForecastHours = aggregate.Hours,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                });
            }

            await context.SaveChangesAsync().ConfigureAwait(false);

            var actualHoursByFiscalYear = await LoadActualHoursAsync(context, engagementIds, fiscalYears).ConfigureAwait(false);
            var actualHoursByEngagement = AggregateActualsByEngagement(actualHoursByFiscalYear);

            var rows = BuildForecastRows(
                aggregatesByKey,
                engagementLookup,
                fiscalYears,
                budgetLookup,
                actualHoursByFiscalYear,
                actualHoursByEngagement);

            var (riskCount, overrunCount) = CountInconsistencies(rows);

            return new StaffAllocationForecastUpdateResult(
                ProcessedRecords: records.Count,
                UpdatedEngagements: engagementIds.Count,
                MissingEngagements: missingEngagements.ToList(),
                MissingBudgets: missingBudgets.ToList(),
                UnknownRanks: unknownRanks.ToList(),
                Rows: rows,
                RiskCount: riskCount,
                OverrunCount: overrunCount);
        }

        public async Task<IReadOnlyList<ForecastAllocationRow>> GetCurrentForecastAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);

            var storedForecasts = await context.StaffAllocationForecasts
                .AsNoTracking()
                .ToListAsync()
                .ConfigureAwait(false);

            if (storedForecasts.Count == 0)
            {
                return Array.Empty<ForecastAllocationRow>();
            }

            var engagementIds = storedForecasts
                .Select(f => f.EngagementId)
                .Distinct()
                .ToList();

            var fiscalYearIds = storedForecasts
                .Select(f => f.FiscalYearId)
                .Distinct()
                .ToList();

            var engagementLookup = await context.Engagements
                .Include(e => e.RankBudgets)
                .Where(e => engagementIds.Contains(e.Id))
                .ToDictionaryAsync(e => e.EngagementId, StringComparer.OrdinalIgnoreCase)
                .ConfigureAwait(false);

            var engagementById = engagementLookup
                .Values
                .ToDictionary(e => e.Id);

            var fiscalYears = await context.FiscalYears
                .AsNoTracking()
                .Where(fy => fiscalYearIds.Contains(fy.Id))
                .ToDictionaryAsync(fy => fy.Id)
                .ConfigureAwait(false);

            var budgetLookup = await context.EngagementRankBudgets
                .Where(rb => engagementIds.Contains(rb.EngagementId))
                .AsNoTracking()
                .ToDictionaryAsync(rb => new EngagementRankKey(rb.EngagementId, rb.RankName), rb => rb, EngagementRankKeyComparer.Instance)
                .ConfigureAwait(false);

            var actualHoursByFiscalYear = await LoadActualHoursAsync(context, engagementIds, fiscalYears).ConfigureAwait(false);
            var actualHoursByEngagement = AggregateActualsByEngagement(actualHoursByFiscalYear);

            var aggregates = storedForecasts
                .Select(f =>
                {
                    engagementById.TryGetValue(f.EngagementId, out var engagement);
                    return new ForecastAggregateResolved(
                        f.EngagementId,
                        engagement?.EngagementId ?? string.Empty,
                        engagement?.Description ?? string.Empty,
                        f.FiscalYearId,
                        NormalizeRank(f.RankName),
                        f.ForecastHours);
                })
                .ToList();

            return BuildForecastRows(
                aggregates,
                engagementLookup,
                fiscalYears,
                budgetLookup,
                actualHoursByFiscalYear,
                actualHoursByEngagement);
        }

        private static IReadOnlyDictionary<int, decimal> AggregateActualsByEngagement(
            IReadOnlyDictionary<(int EngagementId, int FiscalYearId), decimal> actualsByFiscalYear)
        {
            var totals = new Dictionary<int, decimal>();
            foreach (var (key, value) in actualsByFiscalYear)
            {
                if (totals.TryGetValue(key.EngagementId, out var current))
                {
                    totals[key.EngagementId] = current + value;
                }
                else
                {
                    totals[key.EngagementId] = value;
                }
            }

            return totals;
        }

        private async Task<Dictionary<(int EngagementId, int FiscalYearId), decimal>> LoadActualHoursAsync(
            ApplicationDbContext context,
            IReadOnlyCollection<int> engagementIds,
            IReadOnlyDictionary<int, FiscalYear> fiscalYears)
        {
            if (engagementIds.Count == 0)
            {
                return new Dictionary<(int, int), decimal>();
            }

            var actualEntries = await context.ActualsEntries
                .AsNoTracking()
                .Where(a => engagementIds.Contains(a.EngagementId))
                .Select(a => new { a.EngagementId, a.Date, a.Hours })
                .ToListAsync()
                .ConfigureAwait(false);

            var actuals = new Dictionary<(int, int), decimal>();

            foreach (var entry in actualEntries)
            {
                foreach (var fiscalYear in fiscalYears.Values)
                {
                    if (entry.Date >= fiscalYear.StartDate && entry.Date <= fiscalYear.EndDate)
                    {
                        var key = (entry.EngagementId, fiscalYear.Id);
                        if (actuals.TryGetValue(key, out var current))
                        {
                            actuals[key] = current + entry.Hours;
                        }
                        else
                        {
                            actuals[key] = entry.Hours;
                        }

                        break;
                    }
                }
            }

            return actuals;
        }

        private IReadOnlyList<ForecastAllocationRow> BuildForecastRows(
            IReadOnlyList<ForecastAggregateResolved> aggregates,
            IReadOnlyDictionary<string, Engagement> engagementLookup,
            IReadOnlyDictionary<int, FiscalYear> fiscalYears,
            IReadOnlyDictionary<EngagementRankKey, EngagementRankBudget> budgetLookup,
            IReadOnlyDictionary<(int EngagementId, int FiscalYearId), decimal> actualsByFiscalYear,
            IReadOnlyDictionary<int, decimal> actualsByEngagement)
        {
            var rows = new List<ForecastAllocationRow>(aggregates.Count);

            foreach (var aggregate in aggregates.OrderBy(a => a.EngagementCode, StringComparer.OrdinalIgnoreCase)
                                                 .ThenBy(a => a.FiscalYearId)
                                                 .ThenBy(a => a.Rank, StringComparer.OrdinalIgnoreCase))
            {
                if (!engagementLookup.TryGetValue(aggregate.EngagementCode, out var engagement))
                {
                    _logger.LogWarning(
                        "Skipping forecast row for engagement {EngagementCode} because the engagement is missing.",
                        aggregate.EngagementCode);
                    continue;
                }

                if (!fiscalYears.TryGetValue(aggregate.FiscalYearId, out var fiscalYear))
                {
                    _logger.LogWarning(
                        "Skipping forecast row for engagement {EngagementCode} because fiscal year {FiscalYearId} is missing.",
                        aggregate.EngagementCode,
                        aggregate.FiscalYearId);
                    continue;
                }

                var budgetKey = new EngagementRankKey(engagement.Id, aggregate.Rank);
                var budgetHours = budgetLookup.TryGetValue(budgetKey, out var budget)
                    ? budget.Hours
                    : 0m;

                var forecastHours = aggregate.Hours;
                var actualsHours = actualsByFiscalYear.TryGetValue((engagement.Id, fiscalYear.Id), out var actual)
                    ? actual
                    : 0m;

                var actualsTotal = actualsByEngagement.TryGetValue(engagement.Id, out var totalActuals)
                    ? totalActuals
                    : 0m;

                var availableHours = budgetHours - forecastHours;
                var availableToActuals = engagement.InitialHoursBudget - actualsTotal;

                var status = EvaluateStatus(
                    engagement,
                    aggregate.Rank,
                    forecastHours,
                    budgetHours,
                    actualsHours,
                    actualsTotal,
                    availableToActuals);

                rows.Add(new ForecastAllocationRow(
                    engagement.Id,
                    engagement.EngagementId,
                    engagement.Description,
                    fiscalYear.Id,
                    fiscalYear.Name,
                    aggregate.Rank,
                    budgetHours,
                    actualsHours,
                    forecastHours,
                    availableHours,
                    availableToActuals,
                    status));
            }

            return new ReadOnlyCollection<ForecastAllocationRow>(rows);
        }

        private string EvaluateStatus(
            Engagement engagement,
            string rank,
            decimal forecastHours,
            decimal budgetHours,
            decimal actualsHours,
            decimal totalActuals,
            decimal availableToActuals)
        {
            if (totalActuals > engagement.InitialHoursBudget + 0.01m)
            {
                _logger.LogWarning(
                    "Actual hours exceed the initial budget for engagement {EngagementId} (rank {Rank}).",
                    engagement.EngagementId,
                    rank);
                return "Estouro";
            }

            if (forecastHours > budgetHours + 0.01m || forecastHours > availableToActuals + 0.01m)
            {
                _logger.LogWarning(
                    "Forecast hours exceed available hours for engagement {EngagementId} (rank {Rank}).",
                    engagement.EngagementId,
                    rank);
                return "Risco";
            }

            return "OK";
        }

        private static (int RiskCount, int OverrunCount) CountInconsistencies(IEnumerable<ForecastAllocationRow> rows)
        {
            var riskCount = 0;
            var overrunCount = 0;

            foreach (var row in rows)
            {
                if (string.Equals(row.Status, "Risco", StringComparison.OrdinalIgnoreCase))
                {
                    riskCount++;
                }
                else if (string.Equals(row.Status, "Estouro", StringComparison.OrdinalIgnoreCase))
                {
                    overrunCount++;
                }
            }

            return (riskCount, overrunCount);
        }

        private static string NormalizeRank(string? rank)
        {
            return string.IsNullOrWhiteSpace(rank)
                ? "Unspecified"
                : rank.Trim();
        }

        private sealed record ForecastAggregate(string EngagementCode, string Rank, int FiscalYearId, decimal Hours);

        private sealed record ForecastAggregateResolved(
            int EngagementId,
            string EngagementCode,
            string EngagementName,
            int FiscalYearId,
            string Rank,
            decimal Hours);

        private record struct ForecastAggregateKey(int EngagementId, int FiscalYearId, string Rank);

        private sealed class ForecastAggregateKeyComparer : IEqualityComparer<ForecastAggregateKey>
        {
            public static ForecastAggregateKeyComparer Instance { get; } = new();

            public bool Equals(ForecastAggregateKey x, ForecastAggregateKey y)
            {
                return x.EngagementId == y.EngagementId
                    && x.FiscalYearId == y.FiscalYearId
                    && string.Equals(x.Rank, y.Rank, StringComparison.OrdinalIgnoreCase);
            }

            public int GetHashCode(ForecastAggregateKey obj)
            {
                return HashCode.Combine(obj.EngagementId, obj.FiscalYearId, obj.Rank?.ToUpperInvariant() ?? string.Empty);
            }
        }

        private record struct EngagementRankKey(int EngagementId, string Rank);

        private sealed class EngagementRankKeyComparer : IEqualityComparer<EngagementRankKey>
        {
            public static EngagementRankKeyComparer Instance { get; } = new();

            public bool Equals(EngagementRankKey x, EngagementRankKey y)
            {
                return x.EngagementId == y.EngagementId
                    && string.Equals(x.Rank, y.Rank, StringComparison.OrdinalIgnoreCase);
            }

            public int GetHashCode(EngagementRankKey obj)
            {
                return HashCode.Combine(obj.EngagementId, obj.Rank?.ToUpperInvariant() ?? string.Empty);
            }
        }
    }
}
