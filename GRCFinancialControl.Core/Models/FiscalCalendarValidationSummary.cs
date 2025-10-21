using System.Collections.Generic;

namespace GRCFinancialControl.Core.Models
{
    public sealed record FiscalYearValidationReport(int FiscalYearId, string FiscalYearName, IReadOnlyList<string> Issues)
    {
        public bool HasIssues => Issues.Count > 0;
    }

    public sealed record FiscalCalendarValidationSummary(
        int FiscalYearsProcessed,
        int ClosingPeriodsProcessed,
        int CorrectionsApplied,
        IReadOnlyList<FiscalYearValidationReport> IssuesBefore,
        IReadOnlyList<FiscalYearValidationReport> IssuesAfter,
        IReadOnlyList<string> CorrectionsLog)
    {
        public bool IsConsistent => IssuesAfter.Count == 0;
    }
}
