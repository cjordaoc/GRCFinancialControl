using System.Collections.Generic;

namespace GRCFinancialControl.Persistence.Services.Importers
{
    /// <summary>
    /// Encapsulates Full Management Data import operation results with detailed categorization.
    /// </summary>
    public sealed class FullManagementDataImportResult
    {
        public FullManagementDataImportResult(
            string summary,
            int rowsProcessed,
            int engagementsCreated,
            int engagementsUpdated,
            int financialEvolutionUpserts,
            int s4MetadataRefreshes,
            IReadOnlyCollection<string> manualOnlySkips,
            IReadOnlyCollection<string> lockedFiscalYearSkips,
            IReadOnlyCollection<string> missingClosingPeriodSkips,
            IReadOnlyCollection<string> missingEngagementSkips,
            IReadOnlyCollection<string> closedEngagementSkips,
            IReadOnlyCollection<string> errors,
            IReadOnlyCollection<string> warningMessages)
        {
            Summary = summary;
            RowsProcessed = rowsProcessed;
            EngagementsCreated = engagementsCreated;
            EngagementsUpdated = engagementsUpdated;
            FinancialEvolutionUpserts = financialEvolutionUpserts;
            S4MetadataRefreshes = s4MetadataRefreshes;
            ManualOnlySkips = manualOnlySkips;
            LockedFiscalYearSkips = lockedFiscalYearSkips;
            MissingClosingPeriodSkips = missingClosingPeriodSkips;
            MissingEngagementSkips = missingEngagementSkips;
            ClosedEngagementSkips = closedEngagementSkips;
            Errors = errors;
            WarningMessages = warningMessages;
        }

        public string Summary { get; }
        public int RowsProcessed { get; }
        public int EngagementsCreated { get; }
        public int EngagementsUpdated { get; }
        public int FinancialEvolutionUpserts { get; }
        public int S4MetadataRefreshes { get; }
        public IReadOnlyCollection<string> ManualOnlySkips { get; }
        public IReadOnlyCollection<string> LockedFiscalYearSkips { get; }
        public IReadOnlyCollection<string> MissingClosingPeriodSkips { get; }
        public IReadOnlyCollection<string> MissingEngagementSkips { get; }
        public IReadOnlyCollection<string> ClosedEngagementSkips { get; }
        public IReadOnlyCollection<string> Errors { get; }
        public IReadOnlyCollection<string> WarningMessages { get; }
    }
}
