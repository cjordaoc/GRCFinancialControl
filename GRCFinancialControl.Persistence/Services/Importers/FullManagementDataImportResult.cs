using System.Collections.Generic;

namespace GRCFinancialControl.Persistence.Services.Importers
{
    public sealed class FullManagementDataImportResult
    {
        public FullManagementDataImportResult(
            string summary,
            int rowsProcessed,
            int engagementsCreated,
            int engagementsUpdated,
            int financialEvolutionUpserts,
            IReadOnlyCollection<string> manualOnlySkips,
            IReadOnlyCollection<string> lockedFiscalYearSkips,
            IReadOnlyCollection<string> missingClosingPeriodSkips,
            IReadOnlyCollection<string> errors)
        {
            Summary = summary;
            RowsProcessed = rowsProcessed;
            EngagementsCreated = engagementsCreated;
            EngagementsUpdated = engagementsUpdated;
            FinancialEvolutionUpserts = financialEvolutionUpserts;
            ManualOnlySkips = manualOnlySkips;
            LockedFiscalYearSkips = lockedFiscalYearSkips;
            MissingClosingPeriodSkips = missingClosingPeriodSkips;
            Errors = errors;
        }

        public string Summary { get; }
        public int RowsProcessed { get; }
        public int EngagementsCreated { get; }
        public int EngagementsUpdated { get; }
        public int FinancialEvolutionUpserts { get; }
        public IReadOnlyCollection<string> ManualOnlySkips { get; }
        public IReadOnlyCollection<string> LockedFiscalYearSkips { get; }
        public IReadOnlyCollection<string> MissingClosingPeriodSkips { get; }
        public IReadOnlyCollection<string> Errors { get; }
    }
}
