using System.Threading.Tasks;
namespace GRCFinancialControl.Persistence.Services.Interfaces
{
    public interface IImportService
    {
        Task<string> ImportBudgetAsync(string filePath);
        Task<Importers.FullManagementDataImportResult> ImportFcsRevenueBacklogAsync(string filePath);
        Task<Importers.FullManagementDataImportResult> ImportFullManagementDataAsync(string filePath);
        Task<string> ImportAllocationPlanningAsync(string filePath);
        Task<string> UpdateStaffAllocationsAsync(string filePath, int closingPeriodId);
    }
}
