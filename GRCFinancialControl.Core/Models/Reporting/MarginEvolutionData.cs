using System;
using System.Collections.Generic;

namespace GRCFinancialControl.Core.Models.Reporting
{
    public class MarginEvolutionData
    {
        public string EngagementName { get; set; } = string.Empty;
        public List<MarginDataPoint> MarginDataPoints { get; set; } = new List<MarginDataPoint>();
    }

    public class MarginDataPoint
    {
        public string ClosingPeriodName { get; set; } = string.Empty;
        public decimal Margin { get; set; }
    }
}