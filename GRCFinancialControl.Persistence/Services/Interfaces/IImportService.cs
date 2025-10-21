using System.Threading.Tasks;
using GRCFinancialControl.Persistence.Services.Importers.StaffAllocations;

namespace GRCFinancialControl.Persistence.Services.Interfaces
{
    public interface IImportService
    {
        Task<string> ImportBudgetAsync(string filePath);
        Task<string> ImportActualsAsync(string filePath, int closingPeriodId);
        Task<string> ImportFcsRevenueBacklogAsync(string filePath);
        Task<string> ImportFullManagementDataAsync(string filePath);
        Task<StaffAllocationProcessingResult> AnalyzeStaffAllocationsAsync(string filePath);
    }
}
