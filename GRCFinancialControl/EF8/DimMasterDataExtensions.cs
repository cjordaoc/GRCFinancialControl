using System;

namespace GRCFinancialControl.Data
{
    public partial class DimFiscalYear
    {
        public override string ToString()
        {
            var range = $"{DateFrom:yyyy-MM-dd} - {DateTo:yyyy-MM-dd}";
            return IsActive ? $"{Description} ({range}) [ACTIVE]" : $"{Description} ({range})";
        }
    }

    public static class MeasurementPeriodExtensions
    {
        public static string ToDisplayString(this MeasurementPeriod period)
        {
            if (period == null)
            {
                throw new ArgumentNullException(nameof(period));
            }

            return $"{period.Description} ({period.StartDate:yyyy-MM-dd} - {period.EndDate:yyyy-MM-dd})";
        }
    }
}
