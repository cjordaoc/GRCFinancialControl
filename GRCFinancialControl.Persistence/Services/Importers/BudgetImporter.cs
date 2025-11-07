using System;
using System.IO;
using System.Threading.Tasks;
using GRCFinancialControl.Persistence.Services.Infrastructure;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GRCFinancialControl.Persistence.Services.Importers
{
    /// <summary>
    /// Imports budget Excel workbooks (PLAN INFO + RESOURCING sheets).
    /// Inherits from ImportServiceBase for common Excel loading functionality.
    /// 
    /// Creates/updates:
    /// - Engagements (with initial budget)
    /// - Customers  
    /// - RankBudgets (by fiscal year and rank)
    /// - Employees (with rank mapping)
    /// - RankMappings (active mappings from spreadsheet)
    /// 
    /// TODO: Extract full Budget import logic from ImportService (currently delegates)
    /// </summary>
    public sealed class BudgetImporter : ImportServiceBase
    {
        private readonly IFiscalCalendarConsistencyService _fiscalCalendarConsistencyService;
        private readonly IImportService _legacyImportService;

        public BudgetImporter(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            ILogger<BudgetImporter> logger,
            IFiscalCalendarConsistencyService fiscalCalendarConsistencyService,
            IImportService legacyImportService)
            : base(contextFactory, logger)
        {
            _fiscalCalendarConsistencyService = fiscalCalendarConsistencyService ?? 
                throw new ArgumentNullException(nameof(fiscalCalendarConsistencyService));
            _legacyImportService = legacyImportService ??
                throw new ArgumentNullException(nameof(legacyImportService));
        }

        /// <summary>
        /// Imports a budget workbook and creates/updates engagement with rank budgets.
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
                throw new FileNotFoundException("Budget workbook could not be found.", filePath);
            }

            Logger.LogInformation("Budget import started for file: {FilePath}", filePath);

            // Delegate to legacy ImportService
            // TODO: Extract and implement Budget logic directly here
            var result = await _legacyImportService.ImportBudgetAsync(filePath).ConfigureAwait(false);

            Logger.LogInformation("Budget import completed successfully");
            return result;
        }
    }
}
