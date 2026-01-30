using System.Collections.Generic;
using System.Threading.Tasks;
using GRC.Shared.Core.Models.Core;
using GRC.Shared.Core.Models.Financial;
using GRC.Shared.Core.Models.Allocations;

using GRC.Shared.Core.Models.Assignments;
using GRC.Shared.Core.Models.Lookups;
using GRCFinancialControl.Core.Models;

namespace GRCFinancialControl.Persistence.Services.Interfaces
{
    public interface IManagerAssignmentService
    {
        Task<List<EngagementManagerAssignment>> GetAllAsync();
        Task<List<EngagementManagerAssignment>> GetByEngagementIdAsync(int engagementId);
        Task<List<EngagementManagerAssignment>> GetByManagerIdAsync(int managerId);
        Task<EngagementManagerAssignment?> GetByIdAsync(int id);
        Task AddAsync(EngagementManagerAssignment assignment);
        Task UpdateAsync(EngagementManagerAssignment assignment);
        Task DeleteAsync(int id);
        Task UpdateAssignmentsForEngagementAsync(int engagementId, IEnumerable<int> managerIds);
    }
}
