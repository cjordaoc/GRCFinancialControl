using System.Collections.Generic;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Models.Reporting;

namespace GRCFinancialControl.Persistence.Services.Interfaces
{
    public interface IReportService
    {
        Task<List<PlannedVsActualData>> GetPlannedVsActualDataAsync();
        Task<List<BacklogData>> GetBacklogDataAsync();
    }
}