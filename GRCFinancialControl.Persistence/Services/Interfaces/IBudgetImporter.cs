using System.Threading.Tasks;

namespace GRCFinancialControl.Persistence.Services.Interfaces
{
    /// <summary>
    /// Service for importing budget workbooks (PLAN INFO + RESOURCING).
    /// Creates/updates Engagements, Customers, RankBudgets, and Employees.
    /// </summary>
    public interface IBudgetImporter
    {
        /// <summary>
        /// Imports a budget Excel workbook.
        /// </summary>
        /// <param name="filePath">Path to the budget workbook file.</param>
        /// <returns>Import summary with statistics and warnings.</returns>
        Task<string> ImportAsync(string filePath);
    }
}
