using System.Collections.Generic;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Models;

namespace GRCFinancialControl.Persistence.Services.Interfaces
{
    public interface IPlannedAllocationService
    {
        Task<List<PlannedAllocation>> GetForEngagementAsync(int engagementId);
        Task SaveForEngagementAsync(int engagementId, List<PlannedAllocation> allocations);
    }
}