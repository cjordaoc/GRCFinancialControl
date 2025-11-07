using System;
using System.IO;
using System.Threading.Tasks;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GRCFinancialControl.Persistence.Services.Importers
{
    /// <summary>
    /// Imports allocation planning Excel workbooks for staff assignment forecasts.
    /// Inherits from ImportServiceBase for common Excel loading functionality.
    /// 
    /// Updates:
    /// - SimplifiedStaffAllocation (planned assignments per engagement/week)
    /// 
    /// TODO: Extract full Allocation Planning import logic from ImportService (currently delegates)
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
        /// Imports allocation planning data from Excel workbook.
        /// Currently delegates to legacy ImportService for full implementation.
        /// </summary>
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
            // TODO: Extract and implement Allocation Planning logic directly here
            var result = await _legacyImportService.ImportAllocationPlanningAsync(filePath).ConfigureAwait(false);

            Logger.LogInformation("Allocation Planning import completed successfully");
            return result;
        }
    }
}
