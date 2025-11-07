using System.Collections.Generic;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Models;

namespace GRCFinancialControl.Persistence.Services.Interfaces
{
    /// <summary>
    /// Manages allocation snapshots for revenue and hours allocations.
    /// Provides snapshot-based operations including copying from previous periods,
    /// discrepancy detection, and synchronization with Financial Evolution.
    /// </summary>
    public interface IAllocationSnapshotService
    {
        /// <summary>
        /// Gets revenue allocation snapshot for a specific engagement and closing period.
        /// If no snapshot exists, returns an empty collection.
        /// </summary>
        Task<List<EngagementFiscalYearRevenueAllocation>> GetRevenueAllocationSnapshotAsync(
            int engagementId,
            int closingPeriodId);

        /// <summary>
        /// Gets hours allocation snapshot for a specific engagement and closing period.
        /// If no snapshot exists, returns an empty collection.
        /// </summary>
        Task<List<EngagementRankBudget>> GetHoursAllocationSnapshotAsync(
            int engagementId,
            int closingPeriodId);

        /// <summary>
        /// Creates revenue allocation snapshot for a closing period by copying from the latest previous period.
        /// If no previous period exists, creates empty snapshots for all fiscal years.
        /// </summary>
        /// <returns>The newly created revenue allocations.</returns>
        Task<List<EngagementFiscalYearRevenueAllocation>> CreateRevenueSnapshotFromPreviousPeriodAsync(
            int engagementId,
            int closingPeriodId);

        /// <summary>
        /// Creates hours allocation snapshot for a closing period by copying from the latest previous period.
        /// If no previous period exists, creates empty snapshots for all ranks and fiscal years.
        /// </summary>
        /// <returns>The newly created hours allocations.</returns>
        Task<List<EngagementRankBudget>> CreateHoursSnapshotFromPreviousPeriodAsync(
            int engagementId,
            int closingPeriodId);

        /// <summary>
        /// Saves revenue allocation snapshot for a specific closing period.
        /// Also updates the corresponding Financial Evolution snapshot.
        /// </summary>
        Task SaveRevenueAllocationSnapshotAsync(
            int engagementId,
            int closingPeriodId,
            List<EngagementFiscalYearRevenueAllocation> allocations);

        /// <summary>
        /// Saves hours allocation snapshot for a specific closing period.
        /// Also updates the corresponding Financial Evolution snapshot.
        /// </summary>
        Task SaveHoursAllocationSnapshotAsync(
            int engagementId,
            int closingPeriodId,
            List<EngagementRankBudget> budgets);

        /// <summary>
        /// Detects discrepancies between allocation totals and Financial Evolution imported values.
        /// Returns a collection of discrepancy descriptions for user notification.
        /// </summary>
        Task<AllocationDiscrepancyReport> DetectDiscrepanciesAsync(
            int engagementId,
            int closingPeriodId);
    }

    /// <summary>
    /// Represents discrepancies detected between allocations and Financial Evolution.
    /// </summary>
    public class AllocationDiscrepancyReport
    {
        public bool HasDiscrepancies => RevenueDiscrepancies.Count > 0 || HoursDiscrepancies.Count > 0;
        
        public List<DiscrepancyDetail> RevenueDiscrepancies { get; set; } = new();
        public List<DiscrepancyDetail> HoursDiscrepancies { get; set; } = new();
    }

    /// <summary>
    /// Details about a specific discrepancy.
    /// </summary>
    public class DiscrepancyDetail
    {
        public string Category { get; set; } = string.Empty;
        public string FiscalYearName { get; set; } = string.Empty;
        public decimal AllocatedValue { get; set; }
        public decimal ImportedValue { get; set; }
        public decimal Variance { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
