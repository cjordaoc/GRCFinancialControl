using System.Collections.Generic;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Models;

namespace GRCFinancialControl.Persistence.Services.Interfaces
{
    public interface IPapdAssignmentService
    {
        Task<List<EngagementPapd>> GetByEngagementIdAsync(int engagementId);
        Task UpdateAssignmentsForEngagementAsync(int engagementId, IEnumerable<int> papdIds);
    }
}
