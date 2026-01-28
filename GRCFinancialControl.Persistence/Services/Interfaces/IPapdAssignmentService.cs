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
    public interface IPapdAssignmentService
    {
        Task<List<EngagementPapd>> GetByEngagementIdAsync(int engagementId);
        Task<List<EngagementPapd>> GetByPapdIdAsync(int papdId);
        Task DeleteAsync(int id);
        Task UpdateAssignmentsForEngagementAsync(int engagementId, IEnumerable<int> papdIds);
    }
}
