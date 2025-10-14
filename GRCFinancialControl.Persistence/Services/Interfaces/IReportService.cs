using System.Collections.Generic;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Models.Reporting;

namespace GRCFinancialControl.Persistence.Services.Interfaces
{
    public interface IReportService
    {
        Task<List<PapdContributionData>> GetPapdContributionDataAsync();
        Task<List<FinancialEvolutionPoint>> GetFinancialEvolutionPointsAsync(string engagementId);
    }
}