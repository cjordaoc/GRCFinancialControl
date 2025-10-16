using System;

namespace GRCFinancialControl.Core.Models
{
    public sealed class FiscalYearCloseResult
    {
        public FiscalYearCloseResult(FiscalYear closedFiscalYear, FiscalYear? promotedFiscalYear)
        {
            ClosedFiscalYear = closedFiscalYear ?? throw new ArgumentNullException(nameof(closedFiscalYear));
            PromotedFiscalYear = promotedFiscalYear;
        }

        public FiscalYear ClosedFiscalYear { get; }

        public FiscalYear? PromotedFiscalYear { get; }
    }
}
