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
    public interface IEngagementService
    {
        Task<List<Engagement>> GetAllAsync();
        Task<Engagement?> GetByIdAsync(int id);
        Task<Papd?> GetPapdForDateAsync(int engagementId, System.DateTime date);
        Task AddAsync(Engagement engagement);
        Task UpdateAsync(Engagement engagement);
        Task DeleteAsync(int id);
        Task DeleteDataAsync(int engagementId);
    }
}