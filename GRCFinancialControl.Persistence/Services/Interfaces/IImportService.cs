using System.Threading.Tasks;
namespace GRCFinancialControl.Persistence.Services.Interfaces
{
    public interface IImportService
    {
        Task<string> ImportBudgetAsync(string filePath);
        Task<string> ImportFcsRevenueBacklogAsync(string filePath);
        Task<string> ImportFullManagementDataAsync(string filePath);
        Task<string> ImportAllocationPlanningAsync(string filePath);
        Task<string> UpdateStaffAllocationsAsync(string filePath, int closingPeriodId);
    }
}
