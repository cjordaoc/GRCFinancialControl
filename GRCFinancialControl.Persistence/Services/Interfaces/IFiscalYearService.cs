using System.Collections.Generic;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Models;

namespace GRCFinancialControl.Persistence.Services.Interfaces
{
    public interface IFiscalYearService
    {
        Task<List<FiscalYear>> GetAllAsync();
        Task AddAsync(FiscalYear fiscalYear);
        Task UpdateAsync(FiscalYear fiscalYear);
        Task DeleteAsync(int id);
    }
}