using System.Collections.Generic;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Models;

namespace GRCFinancialControl.Persistence.Services.Interfaces
{
    public interface IPlannedAllocationService
    {
        Task<List<PlannedAllocation>> GetAllocationsForEngagementAsync(int engagementId);
        Task SaveAllocationsForEngagementAsync(int engagementId, List<PlannedAllocation> allocations);
    }
}