using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GRCFinancialControl.Persistence.Services
{
    /// <summary>
    /// Manages allocation snapshots with copy-from-previous-period logic and discrepancy detection.
    /// Follows OOP principles: single responsibility, encapsulation, and reusability.
    /// Performance: Uses dictionary lookups, async/await with ConfigureAwait(false), and batch operations.
    /// </summary>
    public sealed class AllocationSnapshotService : IAllocationSnapshotService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ILogger<AllocationSnapshotService> _logger;

        public AllocationSnapshotService(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            ILogger<AllocationSnapshotService> logger)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<List<EngagementFiscalYearRevenueAllocation>> GetRevenueAllocationSnapshotAsync(
            int engagementId,
            int closingPeriodId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);

            return await context.EngagementFiscalYearRevenueAllocations
                .AsNoTracking()
                .Include(a => a.FiscalYear)
                .Include(a => a.ClosingPeriod)
                .Where(a => a.EngagementId == engagementId && a.ClosingPeriodId == closingPeriodId)
                .OrderBy(a => a.FiscalYear!.StartDate)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        public async Task<List<EngagementRankBudget>> GetHoursAllocationSnapshotAsync(
            int engagementId,
            int closingPeriodId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);

            return await context.EngagementRankBudgets
                .AsNoTracking()
                .Include(b => b.FiscalYear)
                .Include(b => b.ClosingPeriod)
                .Where(b => b.EngagementId == engagementId && b.ClosingPeriodId == closingPeriodId)
                .OrderBy(b => b.FiscalYear!.StartDate)
                .ThenBy(b => b.RankName)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        public async Task<List<EngagementFiscalYearRevenueAllocation>> CreateRevenueSnapshotFromPreviousPeriodAsync(
            int engagementId,
            int closingPeriodId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);

            // Get the target closing period
            var targetPeriod = await context.ClosingPeriods
                .AsNoTracking()
                .FirstOrDefaultAsync(cp => cp.Id == closingPeriodId)
                .ConfigureAwait(false);

            if (targetPeriod == null)
            {
                throw new InvalidOperationException($"Closing period {closingPeriodId} not found.");
            }

            // Get fiscal years for the snapshot
            var fiscalYears = await context.FiscalYears
                .AsNoTracking()
                .OrderBy(fy => fy.StartDate)
                .ToListAsync()
                .ConfigureAwait(false);

            // Find the latest previous closing period
            var previousPeriod = await context.ClosingPeriods
                .AsNoTracking()
                .Where(cp => cp.PeriodEnd < targetPeriod.PeriodEnd)
                .OrderByDescending(cp => cp.PeriodEnd)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);

            List<EngagementFiscalYearRevenueAllocation> newAllocations;

            if (previousPeriod != null)
            {
                // Copy from previous period
                var previousAllocations = await context.EngagementFiscalYearRevenueAllocations
                    .AsNoTracking()
                    .Where(a => a.EngagementId == engagementId && a.ClosingPeriodId == previousPeriod.Id)
                    .ToListAsync()
                    .ConfigureAwait(false);

                newAllocations = previousAllocations.Select(prev => new EngagementFiscalYearRevenueAllocation
                {
                    EngagementId = engagementId,
                    FiscalYearId = prev.FiscalYearId,
                    ClosingPeriodId = closingPeriodId,
                    ToGoValue = prev.ToGoValue,
                    ToDateValue = prev.ToDateValue,
                    LastUpdateDate = DateTime.UtcNow.Date,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }).ToList();

                _logger.LogInformation(
                    "Copied {Count} revenue allocations from period {PreviousPeriod} to period {TargetPeriod} for engagement {EngagementId}.",
                    newAllocations.Count, previousPeriod.Name, targetPeriod.Name, engagementId);
            }
            else
            {
                // No previous period - create empty allocations for all fiscal years
                newAllocations = fiscalYears.Select(fy => new EngagementFiscalYearRevenueAllocation
                {
                    EngagementId = engagementId,
                    FiscalYearId = fy.Id,
                    ClosingPeriodId = closingPeriodId,
                    ToGoValue = 0m,
                    ToDateValue = 0m,
                    LastUpdateDate = DateTime.UtcNow.Date,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }).ToList();

                _logger.LogInformation(
                    "Created {Count} empty revenue allocations for period {TargetPeriod} for engagement {EngagementId} (no previous period found).",
                    newAllocations.Count, targetPeriod.Name, engagementId);
            }

            return newAllocations;
        }

        public async Task<List<EngagementRankBudget>> CreateHoursSnapshotFromPreviousPeriodAsync(
            int engagementId,
            int closingPeriodId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);

            // Get the target closing period
            var targetPeriod = await context.ClosingPeriods
                .AsNoTracking()
                .FirstOrDefaultAsync(cp => cp.Id == closingPeriodId)
                .ConfigureAwait(false);

            if (targetPeriod == null)
            {
                throw new InvalidOperationException($"Closing period {closingPeriodId} not found.");
            }

            // Find the latest previous closing period
            var previousPeriod = await context.ClosingPeriods
                .AsNoTracking()
                .Where(cp => cp.PeriodEnd < targetPeriod.PeriodEnd)
                .OrderByDescending(cp => cp.PeriodEnd)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);

            List<EngagementRankBudget> newBudgets;

            if (previousPeriod != null)
            {
                // Copy from previous period
                var previousBudgets = await context.EngagementRankBudgets
                    .AsNoTracking()
                    .Where(b => b.EngagementId == engagementId && b.ClosingPeriodId == previousPeriod.Id)
                    .ToListAsync()
                    .ConfigureAwait(false);

                newBudgets = previousBudgets.Select(prev => new EngagementRankBudget
                {
                    EngagementId = engagementId,
                    FiscalYearId = prev.FiscalYearId,
                    ClosingPeriodId = closingPeriodId,
                    RankName = prev.RankName,
                    BudgetHours = prev.BudgetHours,
                    ConsumedHours = prev.ConsumedHours,
                    AdditionalHours = prev.AdditionalHours,
                    RemainingHours = prev.RemainingHours,
                    Status = prev.Status,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                }).ToList();

                _logger.LogInformation(
                    "Copied {Count} hours budgets from period {PreviousPeriod} to period {TargetPeriod} for engagement {EngagementId}.",
                    newBudgets.Count, previousPeriod.Name, targetPeriod.Name, engagementId);
            }
            else
            {
                // No previous period - return empty list
                // Hours allocations are typically created by imports or manual additions
                newBudgets = new List<EngagementRankBudget>();

                _logger.LogInformation(
                    "No previous hours allocations found for engagement {EngagementId} - returning empty snapshot.",
                    engagementId);
            }

            return newBudgets;
        }

        public async Task SaveRevenueAllocationSnapshotAsync(
            int engagementId,
            int closingPeriodId,
            List<EngagementFiscalYearRevenueAllocation> allocations)
        {
            ArgumentNullException.ThrowIfNull(allocations);

            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);

            // Validate closing period exists and is not locked
            var closingPeriod = await context.ClosingPeriods
                .Include(cp => cp.FiscalYear)
                .FirstOrDefaultAsync(cp => cp.Id == closingPeriodId)
                .ConfigureAwait(false);

            if (closingPeriod == null)
            {
                throw new InvalidOperationException($"Closing period {closingPeriodId} not found.");
            }

            if (closingPeriod.FiscalYear?.IsLocked ?? false)
            {
                throw new InvalidOperationException(
                    $"Cannot save revenue allocations for locked fiscal year '{closingPeriod.FiscalYear.Name}'. Unlock it before making changes.");
            }

            // Remove existing allocations for this snapshot
            var existingAllocations = await context.EngagementFiscalYearRevenueAllocations
                .Where(a => a.EngagementId == engagementId && a.ClosingPeriodId == closingPeriodId)
                .ToListAsync()
                .ConfigureAwait(false);

            context.EngagementFiscalYearRevenueAllocations.RemoveRange(existingAllocations);

            // Add new allocations
            var nowUtc = DateTime.UtcNow;
            foreach (var allocation in allocations)
            {
                allocation.EngagementId = engagementId;
                allocation.ClosingPeriodId = closingPeriodId;
                allocation.UpdatedAt = nowUtc;
                allocation.LastUpdateDate = nowUtc.Date;
                
                if (allocation.CreatedAt == default)
                {
                    allocation.CreatedAt = nowUtc;
                }

                context.EngagementFiscalYearRevenueAllocations.Add(allocation);
            }

            await context.SaveChangesAsync().ConfigureAwait(false);

            // Synchronize with Financial Evolution
            await SyncRevenueToFinancialEvolutionAsync(context, engagementId, closingPeriodId, allocations)
                .ConfigureAwait(false);

            await context.SaveChangesAsync().ConfigureAwait(false);

            _logger.LogInformation(
                "Saved {Count} revenue allocations for engagement {EngagementId} and period {ClosingPeriodId}.",
                allocations.Count, engagementId, closingPeriodId);
        }

        public async Task SaveHoursAllocationSnapshotAsync(
            int engagementId,
            int closingPeriodId,
            List<EngagementRankBudget> budgets)
        {
            ArgumentNullException.ThrowIfNull(budgets);

            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);

            // Validate closing period exists and is not locked
            var closingPeriod = await context.ClosingPeriods
                .Include(cp => cp.FiscalYear)
                .FirstOrDefaultAsync(cp => cp.Id == closingPeriodId)
                .ConfigureAwait(false);

            if (closingPeriod == null)
            {
                throw new InvalidOperationException($"Closing period {closingPeriodId} not found.");
            }

            if (closingPeriod.FiscalYear?.IsLocked ?? false)
            {
                throw new InvalidOperationException(
                    $"Cannot save hours allocations for locked fiscal year '{closingPeriod.FiscalYear.Name}'. Unlock it before making changes.");
            }

            // Remove existing budgets for this snapshot
            var existingBudgets = await context.EngagementRankBudgets
                .Where(b => b.EngagementId == engagementId && b.ClosingPeriodId == closingPeriodId)
                .ToListAsync()
                .ConfigureAwait(false);

            context.EngagementRankBudgets.RemoveRange(existingBudgets);

            // Add new budgets
            var nowUtc = DateTime.UtcNow;
            foreach (var budget in budgets)
            {
                budget.EngagementId = engagementId;
                budget.ClosingPeriodId = closingPeriodId;
                budget.UpdatedAtUtc = nowUtc;

                if (budget.CreatedAtUtc == default)
                {
                    budget.CreatedAtUtc = nowUtc;
                }

                context.EngagementRankBudgets.Add(budget);
            }

            await context.SaveChangesAsync().ConfigureAwait(false);

            // Synchronize with Financial Evolution
            await SyncHoursToFinancialEvolutionAsync(context, engagementId, closingPeriodId, budgets)
                .ConfigureAwait(false);

            await context.SaveChangesAsync().ConfigureAwait(false);

            _logger.LogInformation(
                "Saved {Count} hours budgets for engagement {EngagementId} and period {ClosingPeriodId}.",
                budgets.Count, engagementId, closingPeriodId);
        }

        public async Task<AllocationDiscrepancyReport> DetectDiscrepanciesAsync(
            int engagementId,
            int closingPeriodId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);

            var report = new AllocationDiscrepancyReport();

            // Get Financial Evolution snapshot
            var financialEvolution = await context.FinancialEvolutions
                .AsNoTracking()
                .Include(fe => fe.FiscalYear)
                .FirstOrDefaultAsync(fe => fe.EngagementId == engagementId &&
                                          fe.ClosingPeriodId == closingPeriodId.ToString())
                .ConfigureAwait(false);

            if (financialEvolution == null)
            {
                // No Financial Evolution snapshot - no discrepancies to detect
                return report;
            }

            // Check Revenue Discrepancies
            var revenueAllocations = await context.EngagementFiscalYearRevenueAllocations
                .AsNoTracking()
                .Include(a => a.FiscalYear)
                .Where(a => a.EngagementId == engagementId && a.ClosingPeriodId == closingPeriodId)
                .ToListAsync()
                .ConfigureAwait(false);

            var totalToGo = revenueAllocations.Sum(a => a.ToGoValue);
            var totalToDate = revenueAllocations.Sum(a => a.ToDateValue);

            if (financialEvolution.RevenueToGoValue.HasValue &&
                Math.Abs(totalToGo - financialEvolution.RevenueToGoValue.Value) > 0.01m)
            {
                report.RevenueDiscrepancies.Add(new DiscrepancyDetail
                {
                    Category = "Revenue To-Go",
                    FiscalYearName = financialEvolution.FiscalYear?.Name ?? "N/A",
                    AllocatedValue = totalToGo,
                    ImportedValue = financialEvolution.RevenueToGoValue.Value,
                    Variance = totalToGo - financialEvolution.RevenueToGoValue.Value,
                    Message = $"Revenue To-Go allocation ({totalToGo:N2}) differs from imported value ({financialEvolution.RevenueToGoValue.Value:N2})."
                });
            }

            if (financialEvolution.RevenueToDateValue.HasValue &&
                Math.Abs(totalToDate - financialEvolution.RevenueToDateValue.Value) > 0.01m)
            {
                report.RevenueDiscrepancies.Add(new DiscrepancyDetail
                {
                    Category = "Revenue To-Date",
                    FiscalYearName = financialEvolution.FiscalYear?.Name ?? "N/A",
                    AllocatedValue = totalToDate,
                    ImportedValue = financialEvolution.RevenueToDateValue.Value,
                    Variance = totalToDate - financialEvolution.RevenueToDateValue.Value,
                    Message = $"Revenue To-Date allocation ({totalToDate:N2}) differs from imported value ({financialEvolution.RevenueToDateValue.Value:N2})."
                });
            }

            // Check Hours Discrepancies
            var hoursBudgets = await context.EngagementRankBudgets
                .AsNoTracking()
                .Where(b => b.EngagementId == engagementId && b.ClosingPeriodId == closingPeriodId)
                .ToListAsync()
                .ConfigureAwait(false);

            var totalBudgetHours = hoursBudgets.Sum(b => b.BudgetHours);
            var totalChargedHours = hoursBudgets.Sum(b => b.ConsumedHours);

            if (financialEvolution.BudgetHours.HasValue &&
                Math.Abs(totalBudgetHours - financialEvolution.BudgetHours.Value) > 0.01m)
            {
                report.HoursDiscrepancies.Add(new DiscrepancyDetail
                {
                    Category = "Budget Hours",
                    FiscalYearName = financialEvolution.FiscalYear?.Name ?? "N/A",
                    AllocatedValue = totalBudgetHours,
                    ImportedValue = financialEvolution.BudgetHours.Value,
                    Variance = totalBudgetHours - financialEvolution.BudgetHours.Value,
                    Message = $"Total budget hours ({totalBudgetHours:N2}) differ from imported value ({financialEvolution.BudgetHours.Value:N2})."
                });
            }

            if (financialEvolution.ChargedHours.HasValue &&
                Math.Abs(totalChargedHours - financialEvolution.ChargedHours.Value) > 0.01m)
            {
                report.HoursDiscrepancies.Add(new DiscrepancyDetail
                {
                    Category = "Charged Hours",
                    FiscalYearName = financialEvolution.FiscalYear?.Name ?? "N/A",
                    AllocatedValue = totalChargedHours,
                    ImportedValue = financialEvolution.ChargedHours.Value,
                    Variance = totalChargedHours - financialEvolution.ChargedHours.Value,
                    Message = $"Total consumed hours ({totalChargedHours:N2}) differ from imported value ({financialEvolution.ChargedHours.Value:N2})."
                });
            }

            return report;
        }

        /// <summary>
        /// Gets or creates a FinancialEvolution snapshot for the given engagement and closing period.
        /// Implements the "get or create" pattern to avoid code duplication.
        /// </summary>
        /// <param name="context">Database context</param>
        /// <param name="engagementId">Engagement identifier</param>
        /// <param name="closingPeriodId">Closing period identifier</param>
        /// <returns>Existing or newly created FinancialEvolution entity</returns>
        private static async Task<FinancialEvolution> GetOrCreateFinancialEvolutionAsync(
            ApplicationDbContext context,
            int engagementId,
            int closingPeriodId)
        {
            var closingPeriodIdStr = closingPeriodId.ToString();

            var evolution = await context.FinancialEvolutions
                .FirstOrDefaultAsync(fe => fe.EngagementId == engagementId &&
                                          fe.ClosingPeriodId == closingPeriodIdStr)
                .ConfigureAwait(false);

            if (evolution == null)
            {
                evolution = new FinancialEvolution
                {
                    EngagementId = engagementId,
                    ClosingPeriodId = closingPeriodIdStr
                };
                context.FinancialEvolutions.Add(evolution);
            }

            return evolution;
        }

        /// <summary>
        /// Synchronizes revenue allocations to Financial Evolution snapshot.
        /// Updates RevenueToGoValue and RevenueToDateValue fields.
        /// </summary>
        private static async Task SyncRevenueToFinancialEvolutionAsync(
            ApplicationDbContext context,
            int engagementId,
            int closingPeriodId,
            List<EngagementFiscalYearRevenueAllocation> allocations)
        {
            var evolution = await GetOrCreateFinancialEvolutionAsync(context, engagementId, closingPeriodId)
                .ConfigureAwait(false);

            // Update revenue fields
            evolution.RevenueToGoValue = allocations.Sum(a => a.ToGoValue);
            evolution.RevenueToDateValue = allocations.Sum(a => a.ToDateValue);
        }

        /// <summary>
        /// Synchronizes hours allocations to Financial Evolution snapshot.
        /// Updates BudgetHours and ChargedHours fields.
        /// </summary>
        private static async Task SyncHoursToFinancialEvolutionAsync(
            ApplicationDbContext context,
            int engagementId,
            int closingPeriodId,
            List<EngagementRankBudget> budgets)
        {
            var evolution = await GetOrCreateFinancialEvolutionAsync(context, engagementId, closingPeriodId)
                .ConfigureAwait(false);

            // Update hours fields
            evolution.BudgetHours = budgets.Sum(b => b.BudgetHours);
            evolution.ChargedHours = budgets.Sum(b => b.ConsumedHours);
            evolution.AdditionalHours = budgets.Sum(b => b.AdditionalHours);
        }
    }
}
