using System;
using System.Collections.Generic;
using System.Linq;
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

        public async Task<List<PlannedVsActualData>> GetPlannedVsActualDataAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var allEngagements = await context.Engagements.ToListAsync();
            var allAllocations = await context.EngagementFiscalYearAllocations.ToListAsync();
            var allActuals = await context.ActualsEntries.Include(ae => ae.Papd).ToListAsync();
            var allFiscalYears = await context.FiscalYears.ToListAsync();

            var plannedHoursByKey = allAllocations
                .ToDictionary(a => (a.EngagementId, a.FiscalYearId), a => a.PlannedHours);

            var reportData = new List<PlannedVsActualData>();

            foreach (var engagement in allEngagements)
            {
                foreach (var fy in allFiscalYears)
                {
                    plannedHoursByKey.TryGetValue((engagement.Id, fy.Id), out var plannedHours);

                    var actualsInYear = allActuals
                        .Where(a => a.EngagementId == engagement.Id && a.Date >= fy.StartDate && a.Date <= fy.EndDate);

                    var actualsByPapd = actualsInYear.GroupBy(a => a.Papd);

                    foreach (var group in actualsByPapd)
                    {
                        reportData.Add(new PlannedVsActualData
                        {
                            EngagementId = engagement.EngagementId,
                            EngagementDescription = engagement.Description,
                            PapdName = group.Key?.Name ?? "Unassigned",
                            FiscalYear = fy.Name,
                            PlannedHours = 0, // Planned hours are not attributed to PAPDs in this model
                            ActualHours = group.Sum(a => a.Hours)
                        });
                    }

                    // Add planned hours as a separate entry if there are no actuals for that combo
                    if (plannedHours > 0 && !actualsByPapd.Any())
                    {
                        reportData.Add(new PlannedVsActualData
                        {
                            EngagementId = engagement.EngagementId,
                            EngagementDescription = engagement.Description,
                            PapdName = "N/A",
                            FiscalYear = fy.Name,
                            PlannedHours = plannedHours,
                            ActualHours = 0
                        });
                    }
                    else if (plannedHours > 0)
                    {
                        reportData.Add(new PlannedVsActualData
                        {
                            EngagementId = engagement.EngagementId,
                            EngagementDescription = engagement.Description,
                            PapdName = "Total",
                            FiscalYear = fy.Name,
                            PlannedHours = plannedHours,
                            ActualHours = actualsInYear.Sum(a => a.Hours)
                        });
                    }
                }
            }

            return reportData;
        }

        public async Task<List<BacklogData>> GetBacklogDataAsync()
        {
            var today = DateTime.Today;
            await using var context = await _contextFactory.CreateDbContextAsync();

            var backlogData = await context.PlannedAllocations
                .Include(pa => pa.Engagement)
                .Include(pa => pa.ClosingPeriod)
                .Where(pa => pa.ClosingPeriod.PeriodStart > today)
                .GroupBy(pa => new { pa.Engagement.EngagementId, pa.Engagement.Description })
                .Select(g => new BacklogData
                {
                    EngagementId = g.Key.EngagementId,
                    EngagementDescription = g.Key.Description,
                    BacklogHours = g.Sum(pa => pa.AllocatedHours)
                })
                .ToListAsync();

            return backlogData;
        }

        public async Task<List<FiscalPerformanceData>> GetFiscalPerformanceDataAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var fiscalYears = await context.FiscalYears.ToListAsync();
            var result = new List<FiscalPerformanceData>();

            foreach (var fy in fiscalYears)
            {
                var engagementsInYear = await context.EngagementFiscalYearAllocations
                    .Where(a => a.FiscalYearId == fy.Id)
                    .Select(a => a.Engagement)
                    .ToListAsync();

                var totalRevenue = engagementsInYear.Sum(e => e.OpeningValue);

                var totalPlannedHours = (decimal)await context.EngagementFiscalYearAllocations
                    .Where(a => a.FiscalYearId == fy.Id)
                    .SumAsync(a => a.PlannedHours);

                var totalActualHours = (decimal)await context.ActualsEntries
                    .Where(a => a.Date >= fy.StartDate && a.Date <= fy.EndDate)
                    .SumAsync(a => a.Hours);

                var papdContributions = await context.Engagements
                    .Where(e => engagementsInYear.Select(ey => ey.Id).Contains(e.Id))
                    .SelectMany(e => e.EngagementPapds)
                    .GroupBy(ep => ep.Papd.Name)
                    .Select(g => new Core.Models.Reporting.PapdContribution
                    {
                        PapdName = g.Key,
                        Revenue = g.Sum(ep => ep.Engagement.OpeningValue)
                    })
                    .ToListAsync();

                result.Add(new FiscalPerformanceData
                {
                    FiscalYearId = fy.Id,
                    FiscalYearName = fy.Name,
                    AreaSalesTarget = fy.AreaSalesTarget,
                    AreaRevenueTarget = fy.AreaRevenueTarget,
                    TotalRevenue = totalRevenue,
                    TotalPlannedHours = totalPlannedHours,
                    TotalActualHours = totalActualHours,
                    PapdContributions = papdContributions
                });
            }

            return result;
        }

        public async Task<List<EngagementPerformanceData>> GetEngagementPerformanceDataAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var engagements = await context.Engagements
                .Include(e => e.RankBudgets)
                .ToListAsync();

            var result = new List<EngagementPerformanceData>();

            foreach (var engagement in engagements)
            {
                var actualHours = await context.ActualsEntries
                    .Where(a => a.EngagementId == engagement.Id)
                    .SumAsync(a => a.Hours);

                result.Add(new EngagementPerformanceData
                {
                    EngagementId = engagement.EngagementId,
                    EngagementDescription = engagement.Description,
                    InitialHoursBudget = engagement.InitialHoursBudget,
                    ActualHours = (decimal)actualHours,
                    RankBudgets = engagement.RankBudgets.Select(rb => new Core.Models.Reporting.RankBudget
                    {
                        RankName = rb.RankName,
                        Hours = rb.Hours
                    }).ToList()
                });
            }

            return result;
        }

        public async Task<List<PapdContributionData>> GetPapdContributionDataAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var papds = await context.Papds.ToListAsync();
            var result = new List<PapdContributionData>();

            foreach (var papd in papds)
            {
                var engagementIds = await context.EngagementPapds
                    .Where(ep => ep.PapdId == papd.Id)
                    .Select(ep => ep.EngagementId)
                    .ToListAsync();

                var revenueContribution = await context.Engagements
                    .Where(e => engagementIds.Contains(e.Id))
                    .SumAsync(e => e.OpeningValue);

                var hoursWorked = await context.ActualsEntries
                    .Include(a => a.ClosingPeriod)
                    .Where(a => a.PapdId == papd.Id)
                    .GroupBy(a => a.ClosingPeriod.Name)
                    .Select(g => new Core.Models.Reporting.HoursWorked
                    {
                        ClosingPeriodName = g.Key,
                        Hours = (decimal)g.Sum(a => a.Hours)
                    })
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

        public async Task<List<TimeAllocationData>> GetTimeAllocationDataAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var plannedHours = await context.PlannedAllocations
                .Include(pa => pa.ClosingPeriod)
                .GroupBy(pa => pa.ClosingPeriod.Name)
                .Select(g => new { ClosingPeriodName = g.Key, Hours = (decimal)g.Sum(pa => pa.AllocatedHours) })
                .ToListAsync();

            var actualHours = await context.ActualsEntries
                .Include(a => a.ClosingPeriod)
                .GroupBy(a => a.ClosingPeriod.Name)
                .Select(g => new { ClosingPeriodName = g.Key, Hours = (decimal)g.Sum(a => a.Hours) })
                .ToListAsync();

            var closingPeriods = plannedHours.Select(p => p.ClosingPeriodName)
                .Union(actualHours.Select(a => a.ClosingPeriodName))
                .Distinct()
                .ToList();

            var result = new List<TimeAllocationData>();

            foreach (var period in closingPeriods)
            {
                result.Add(new TimeAllocationData
                {
                    ClosingPeriodName = period,
                    PlannedHours = plannedHours.FirstOrDefault(p => p.ClosingPeriodName == period)?.Hours ?? 0m,
                    ActualHours = actualHours.FirstOrDefault(a => a.ClosingPeriodName == period)?.Hours ?? 0m
                });
            }

            return result;
        }

        public async Task<StrategicKpiData> GetStrategicKpiDataAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var fiscalYears = await context.FiscalYears.ToListAsync();
            var totalSalesTarget = fiscalYears.Sum(fy => fy.AreaSalesTarget);
            var totalRevenueTarget = fiscalYears.Sum(fy => fy.AreaRevenueTarget);

            var totalRevenue = await context.Engagements.SumAsync(e => e.OpeningValue);

            var remainingMonths = fiscalYears.Where(fy => fy.EndDate > DateTime.Today)
                                             .Select(fy => ((fy.EndDate.Year - DateTime.Today.Year) * 12) + fy.EndDate.Month - DateTime.Today.Month)
                                             .Sum();

            decimal avgMonthlySalesNeeded = remainingMonths > 0
                ? (totalSalesTarget - totalRevenue) / remainingMonths
                : 0m;

            // Placeholder for PAPD Contribution %
            var papdContributionPercent = 0m;

            // Placeholder for TER Required for Goal
            var terRequiredForGoal = 0m;

            return new StrategicKpiData
            {
                PercentSalesTargetAchieved = totalSalesTarget > 0 ? (totalRevenue / totalSalesTarget) * 100 : 0m,
                PercentRevenueTargetAchieved = totalRevenueTarget > 0 ? (totalRevenue / totalRevenueTarget) * 100 : 0m,
                AvgMonthlySalesNeeded = avgMonthlySalesNeeded,
                PapdContributionPercent = papdContributionPercent,
                TerRequiredForGoal = terRequiredForGoal
            };
        }

        public async Task<List<MarginEvolutionData>> GetMarginEvolutionDataAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var engagements = await context.Engagements
                .Include(e => e.MarginEvolutions)
                .ThenInclude(me => me.ClosingPeriod)
                .ToListAsync();

            var result = new List<MarginEvolutionData>();

            foreach (var engagement in engagements)
            {
                var marginDataPoints = engagement.MarginEvolutions
                    .Where(me => me.ClosingPeriod != null)
                    .OrderBy(me => me.ClosingPeriod!.PeriodStart)
                    .Select(me => new MarginDataPoint
                    {
                        ClosingPeriodName = me.ClosingPeriod!.Name,
                        Margin = me.MarginPercentage
                    })
                    .ToList();

                result.Add(new MarginEvolutionData
                {
                    EngagementName = engagement.Description,
                    MarginDataPoints = marginDataPoints
                });
            }

            return result;
        }
    }
}