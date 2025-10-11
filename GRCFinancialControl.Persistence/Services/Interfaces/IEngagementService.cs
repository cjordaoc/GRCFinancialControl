using System.Collections.Generic;
using System.Threading.Tasks;
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
    }
}