using System.Collections.Generic;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Models;

namespace GRCFinancialControl.Persistence.Services.Interfaces
{
    public interface IHoursAllocationService
    {
        Task<HoursAllocationSnapshot> GetAllocationAsync(int engagementId);
        Task<HoursAllocationSnapshot> SaveAsync(int engagementId, IEnumerable<HoursAllocationCellUpdate> updates);
        Task<HoursAllocationSnapshot> AddRankAsync(int engagementId, string rankName);
        Task DeleteRankAsync(int engagementId, string rankName);
    }
}
