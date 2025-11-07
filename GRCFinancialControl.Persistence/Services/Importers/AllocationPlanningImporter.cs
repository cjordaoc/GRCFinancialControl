using System;
using System.IO;
using System.Threading.Tasks;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GRCFinancialControl.Persistence.Services.Importers
{
    /// <summary>
    /// Imports allocation planning and staff allocation workbooks.
    /// Handles BOTH allocation planning (live budgets) and historical snapshots.
    /// Inherits from ImportServiceBase for common Excel loading functionality.
    /// 
    /// Processes "Alocações_Staff" worksheet for:
    /// 1. ImportAsync(filePath) - Updates EngagementRankBudgets (live planning data)
    /// 2. UpdateHistoryAsync(filePath, closingPeriodId) - Updates EngagementRankBudgetHistory (period snapshots)
    /// 
    /// TODO: Extract full logic from ImportService (currently delegates for Phase 1)
    /// </summary>
    public sealed class AllocationPlanningImporter : ImportServiceBase
    {
        private readonly IImportService _legacyImportService;

        public AllocationPlanningImporter(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            ILogger<AllocationPlanningImporter> logger,
            IImportService legacyImportService)
            : base(contextFactory, logger)
        {
            _legacyImportService = legacyImportService ??
                throw new ArgumentNullException(nameof(legacyImportService));
        }

        /// <summary>
        /// Imports allocation planning data and updates live EngagementRankBudgets.
        /// Processes "Alocações_Staff" worksheet.
        /// </summary>
        /// <param name="filePath">Path to Excel workbook</param>
        /// <returns>Import summary message</returns>
        public async Task<string> ImportAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path must be provided.", nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Allocation planning workbook could not be found.", filePath);
            }

            Logger.LogInformation("Allocation Planning import started for file: {FilePath}", filePath);

            // Delegate to legacy ImportService  
            // TODO: Extract and implement full logic directly here
            var result = await _legacyImportService.ImportAllocationPlanningAsync(filePath).ConfigureAwait(false);

            Logger.LogInformation("Allocation Planning import completed successfully");
            return result;
        }

        /// <summary>
        /// Updates staff allocation history for a specific closing period.
        /// Processes "Alocações_Staff" worksheet and creates historical snapshots.
        /// </summary>
        /// <param name="filePath">Path to Excel workbook</param>
        /// <param name="closingPeriodId">Closing period identifier for historical snapshot</param>
        /// <returns>Update summary message</returns>
        public async Task<string> UpdateHistoryAsync(string filePath, int closingPeriodId)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path must be provided.", nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Staff allocation workbook could not be found.", filePath);
            }

            if (closingPeriodId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(closingPeriodId), closingPeriodId, 
                    "Closing period identifier must be positive.");
            }

            Logger.LogInformation(
                "Staff Allocation history update started for file: {FilePath}, closing period: {ClosingPeriodId}", 
                filePath, closingPeriodId);

            // Delegate to legacy ImportService
            // TODO: Extract and implement UpdateStaffAllocationsAsync logic directly here
            var result = await _legacyImportService.UpdateStaffAllocationsAsync(filePath, closingPeriodId).ConfigureAwait(false);

            Logger.LogInformation("Staff Allocation history update completed successfully");
            return result;
        }
    }
}
