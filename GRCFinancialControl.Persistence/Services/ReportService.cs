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
        private readonly ApplicationDbContext _context;
        private readonly IEngagementService _engagementService;

        public ReportService(ApplicationDbContext context, IEngagementService engagementService)
        {
            _context = context;
            _engagementService = engagementService;
        }

        public async Task<List<PlannedVsActualData>> GetPlannedVsActualDataAsync()
        {
            var allEngagements = await _context.Engagements.ToListAsync();
            var allAllocations = await _context.PlannedAllocations.Include(pa => pa.ClosingPeriod).ToListAsync();
            var allActuals = await _context.ActualsEntries.Include(ae => ae.Papd).ToListAsync();
            var allFiscalYears = await _context.ClosingPeriods.OfType<FiscalYear>().ToListAsync();

            var reportData = new List<PlannedVsActualData>();

            foreach (var engagement in allEngagements)
            {
                foreach (var fy in allFiscalYears)
                {
                    var plannedHours = allAllocations
                        .Where(a => a.EngagementId == engagement.Id && a.ClosingPeriodId == fy.Id)
                        .Sum(a => a.AllocatedHours);

                    var actualsInYear = allActuals
                        .Where(a => a.EngagementId == engagement.Id && a.Date >= fy.PeriodStart && a.Date <= fy.PeriodEnd);

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
                }
            }

            return reportData;
        }

        public async Task<List<BacklogData>> GetBacklogDataAsync()
        {
            var today = DateTime.Today;
            var backlogData = await _context.PlannedAllocations
                .Include(pa => pa.Engagement)
                .Include(pa => pa.ClosingPeriod)
                .Where(pa => pa.ClosingPeriod.Discriminator == nameof(FiscalYear) && pa.ClosingPeriod.PeriodStart > today)
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