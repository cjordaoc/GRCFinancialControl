using System.Collections.Generic;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Models;

namespace GRCFinancialControl.Persistence.Services.Interfaces
{
    public interface IClosingPeriodService
    {
        Task<List<ClosingPeriod>> GetAllAsync();
        Task AddAsync(ClosingPeriod period);
        Task UpdateAsync(ClosingPeriod period);
        Task DeleteAsync(int id);
    }
}
