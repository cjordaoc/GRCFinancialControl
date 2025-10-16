using System.Collections.Generic;
using System;
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
        Task DeleteDataAsync(int fiscalYearId);
        Task<DateTime?> LockAsync(int fiscalYearId, string lockedBy);
        Task<DateTime?> UnlockAsync(int fiscalYearId, string unlockedBy);
        Task<FiscalYearCloseResult> CloseAsync(int fiscalYearId, string closedBy);
    }
}
