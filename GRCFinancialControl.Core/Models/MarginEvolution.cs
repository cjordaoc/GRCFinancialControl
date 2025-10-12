using System;
using GRCFinancialControl.Core.Enums;

namespace GRCFinancialControl.Core.Models
{
    public class MarginEvolution
    {
        public int Id { get; set; }
        public int EngagementId { get; set; }
        public Engagement Engagement { get; set; } = null!;
        public MarginEvolutionType EntryType { get; set; }
        public decimal MarginPercentage { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? EffectiveDate { get; set; }
        public int? ClosingPeriodId { get; set; }
        public ClosingPeriod? ClosingPeriod { get; set; }
    }
}
