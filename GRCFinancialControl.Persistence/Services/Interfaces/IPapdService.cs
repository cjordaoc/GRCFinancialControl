using System.Collections.Generic;
using System.Threading.Tasks;
using GRC.Shared.Core.Models.Core;
using GRC.Shared.Core.Models.Financial;
using GRC.Shared.Core.Models.Allocations;

using GRCFinancialControl.Persistence.Models.Assignments;
using GRC.Shared.Core.Models.Lookups;
using GRCFinancialControl.Core.Models;

namespace GRCFinancialControl.Persistence.Services.Interfaces
{
    public interface IPapdService
    {
        Task<List<Papd>> GetAllAsync();
        Task<Papd?> GetByIdAsync(int id);
        Task AddAsync(Papd papd);
        Task UpdateAsync(Papd papd);
        Task DeleteAsync(int id);
        Task DeleteDataAsync(int papdId);
        Task AssignEngagementsAsync(int papdId, List<int> engagementIds);
    }
}
