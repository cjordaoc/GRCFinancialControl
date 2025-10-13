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
                .ToDictionary(a => (a.EngagementId, a.FiscalYearId), a => a.Hours);

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
    }
}