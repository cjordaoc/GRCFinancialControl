using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using GRCFinancialControl.Core.Enums;
using System.Linq;

namespace GRCFinancialControl.Core.Models
{
    /// <summary>
    /// Represents a client engagement.
    /// </summary>
    public class Engagement
    {
        public int Id { get; set; }
        public string EngagementId { get; set; } = string.Empty; // Business key from source files
        public string Description { get; set; } = string.Empty;
        public string CustomerKey { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        public decimal? MarginPctBudget { get; set; }
        public decimal? MarginPctEtcp { get; set; }
        public int? EtcpAgeDays { get; set; }
        public DateTime? LatestEtcDate { get; set; }
        public DateTime? NextEtcDate { get; set; }
        public string? StatusText { get; set; }
        public decimal OpeningMargin { get; set; }
        public decimal OpeningValue { get; set; }
        public EngagementStatus Status { get; set; }
        public double TotalPlannedHours { get; set; }
        public decimal InitialHoursBudget { get; set; }
        public decimal ActualHours { get; set; }

        public int? CustomerId { get; set; }
        public Customer? Customer { get; set; }

        public ICollection<EngagementPapd> EngagementPapds { get; set; } = new List<EngagementPapd>();
        public ICollection<EngagementRankBudget> RankBudgets { get; set; } = new List<EngagementRankBudget>();
        public ICollection<MarginEvolution> MarginEvolutions { get; set; } = new List<MarginEvolution>();
        public ICollection<EngagementFiscalYearAllocation> Allocations { get; set; } = new List<EngagementFiscalYearAllocation>();
        [NotMapped]
        public double CurrentHoursAllocation => Allocations.Sum(a => a.Hours);
    }
}