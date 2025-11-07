using System.Threading.Tasks;

namespace GRCFinancialControl.Persistence.Services.Interfaces
{
    /// <summary>
    /// Orchestrates all data import operations.
    /// Delegates to specialized importers for each file type.
    /// </summary>
    public interface IImportService
    {
        /// <summary>
        /// Imports a budget workbook (creates/updates Engagements, Customers, RankBudgets, Employees).
        /// </summary>
        Task<string> ImportBudgetAsync(string filePath, int? closingPeriodId = null);

        /// <summary>
        /// Imports Full Management Data workbook (updates Engagements, FinancialEvolution, RevenueAllocations).
        /// This import combines engagement updates, financial snapshots, and revenue backlog data.
        /// </summary>
        /// <param name="filePath">Path to the Excel file to import</param>
        /// <param name="closingPeriodId">The closing period ID selected by the user for this import</param>
        Task<Importers.FullManagementDataImportResult> ImportFullManagementDataAsync(string filePath, int closingPeriodId);

        /// <summary>
        /// Imports allocation planning workbook (creates/updates HoursAllocations, PlannedAllocations).
        /// </summary>
        Task<string> ImportAllocationPlanningAsync(string filePath);

        /// <summary>
        /// Updates staff allocations for a specific closing period.
        /// </summary>
        Task<string> UpdateStaffAllocationsAsync(string filePath, int closingPeriodId);
    }
}
