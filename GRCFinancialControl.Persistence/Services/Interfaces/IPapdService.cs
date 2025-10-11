using System.Collections.Generic;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Models;

namespace GRCFinancialControl.Persistence.Services.Interfaces
{
    public interface IPapdService
    {
        Task<List<Papd>> GetAllAsync();
        Task AddAsync(Papd papd);
        Task UpdateAsync(Papd papd);
        Task DeleteAsync(int id);
    }
}