using System;
using System.Collections.Generic;
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
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public ReportService(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<List<PapdContributionData>> GetPapdContributionDataAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var papds = await context.Papds.AsNoTracking().ToListAsync();
            var result = new List<PapdContributionData>();

            foreach (var papd in papds)
            {
                var engagementIds = await context.EngagementPapds
                    .AsNoTracking()
                    .Where(ep => ep.PapdId == papd.Id)
                    .Select(ep => ep.EngagementId)
                    .ToListAsync();

                if (engagementIds.Count == 0)
                {
                    result.Add(new PapdContributionData
                    {
                        PapdName = papd.Name,
                        RevenueContribution = 0m,
                        HoursWorked = new List<HoursWorked>()
                    });
                    continue;
                }

                var revenueContribution = await context.Engagements
                    .AsNoTracking()
                    .Where(e => engagementIds.Contains(e.Id))
                    .SumAsync(e => e.OpeningValue);

                var hoursWorked = await context.ActualsEntries
                    .AsNoTracking()
                    .Include(a => a.ClosingPeriod)
                    .Where(a => a.PapdId == papd.Id)
                    .GroupBy(a => a.ClosingPeriod.Name)
                    .Select(g => new HoursWorked
                    {
                        ClosingPeriodName = g.Key,
                        Hours = (decimal)g.Sum(a => a.Hours)
                    })
                    .OrderBy(hw => hw.ClosingPeriodName)
                    .ToListAsync();

                result.Add(new PapdContributionData
                {
                    PapdName = papd.Name,
                    RevenueContribution = revenueContribution,
                    HoursWorked = hoursWorked
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
    }
}
