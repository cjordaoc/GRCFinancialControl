using System.Collections.Generic;
using System.Threading.Tasks;
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
