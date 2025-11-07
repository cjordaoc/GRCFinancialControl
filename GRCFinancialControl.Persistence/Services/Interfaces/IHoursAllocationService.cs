using System.Collections.Generic;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Models;

namespace GRCFinancialControl.Persistence.Services.Interfaces
{
    /// <summary>
    /// Manages hours allocation snapshots per closing period.
    /// All operations now require a closingPeriodId to work with snapshot-based architecture.
    /// </summary>
    public interface IHoursAllocationService
    {
        /// <summary>
        /// Gets hours allocation snapshot for a specific engagement and closing period.
        /// </summary>
        Task<HoursAllocationSnapshot> GetAllocationAsync(int engagementId, int closingPeriodId);
        
        /// <summary>
        /// Saves hours allocation snapshot for a specific closing period.
        /// </summary>
        Task<HoursAllocationSnapshot> SaveAsync(
            int engagementId,
            int closingPeriodId,
            IEnumerable<HoursAllocationCellUpdate> updates,
            IEnumerable<HoursAllocationRowAdjustment> rowAdjustments);
        
        /// <summary>
        /// Adds a new rank to the allocation for a specific closing period.
        /// </summary>
        Task<HoursAllocationSnapshot> AddRankAsync(int engagementId, int closingPeriodId, string rankName);
        
        /// <summary>
        /// Deletes a rank from the allocation for a specific closing period.
        /// </summary>
        Task DeleteRankAsync(int engagementId, int closingPeriodId, string rankName);
    }
}
