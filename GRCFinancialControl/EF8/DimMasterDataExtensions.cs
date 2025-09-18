using System;

namespace GRCFinancialControl.Data
{
    public partial class DimMeasurementPeriod
    {
        public override string ToString()
        {
            var range = $"{StartDate:yyyy-MM-dd} - {EndDate:yyyy-MM-dd}";
            return IsActive ? $"{Description} ({range}) [ACTIVE]" : $"{Description} ({range})";
        }
    }

    public partial class DimFiscalYear
    {
        public override string ToString()
        {
            var range = $"{DateFrom:yyyy-MM-dd} - {DateTo:yyyy-MM-dd}";
            return IsActive ? $"{Description} ({range}) [ACTIVE]" : $"{Description} ({range})";
        }
    }
}
