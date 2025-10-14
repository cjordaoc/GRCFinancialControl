using System.Collections.Generic;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Models.Reporting;

namespace GRCFinancialControl.Persistence.Services.Interfaces
{
    public interface IReportService
    {
        Task<List<PlannedVsActualData>> GetPlannedVsActualDataAsync();
        Task<List<BacklogData>> GetBacklogDataAsync();
        Task<List<FiscalPerformanceData>> GetFiscalPerformanceDataAsync();
        Task<List<EngagementPerformanceData>> GetEngagementPerformanceDataAsync();
        Task<List<PapdContributionData>> GetPapdContributionDataAsync();
        Task<List<TimeAllocationData>> GetTimeAllocationDataAsync();
        Task<StrategicKpiData> GetStrategicKpiDataAsync();
        Task<List<FinancialEvolutionPoint>> GetFinancialEvolutionPointsAsync(string engagementId);
    }
}