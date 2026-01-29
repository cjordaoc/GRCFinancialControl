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
    public interface IClosingPeriodService
    {
        Task<List<ClosingPeriod>> GetAllAsync();
        Task AddAsync(ClosingPeriod period);
        Task UpdateAsync(ClosingPeriod period);
        Task DeleteAsync(int id);
        Task DeleteDataAsync(int closingPeriodId);
        Task SetLockStateAsync(int closingPeriodId, bool isLocked);
    }
}
