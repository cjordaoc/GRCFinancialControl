using System.Threading.Tasks;

namespace GRCFinancialControl.Persistence.Services.Interfaces
{
    /// <summary>
    /// Service for importing allocation planning workbooks.
    /// Creates/updates HoursAllocations and PlannedAllocations.
    /// </summary>
    public interface IAllocationPlanningImporter
    {
        /// <summary>
        /// Imports an allocation planning Excel workbook.
        /// </summary>
        /// <param name="filePath">Path to the allocation planning workbook file.</param>
        /// <param name="closingPeriodId">Optional closing period ID for filtering allocations.</param>
        /// <returns>Import summary with statistics and warnings.</returns>
        Task<string> ImportAsync(string filePath, int? closingPeriodId = null);
    }
}
