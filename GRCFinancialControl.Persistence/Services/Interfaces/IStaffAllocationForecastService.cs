using System.Collections.Generic;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Importers.StaffAllocations;

namespace GRCFinancialControl.Persistence.Services.Interfaces
{
    public interface IStaffAllocationForecastService
    {
        Task<StaffAllocationForecastUpdateResult> UpdateForecastAsync(IReadOnlyList<StaffAllocationTemporaryRecord> records);
        Task<IReadOnlyList<ForecastAllocationRow>> GetCurrentForecastAsync();
        Task SaveEngagementForecastAsync(int engagementId, IReadOnlyList<EngagementForecastUpdateEntry> entries);
    }
}
