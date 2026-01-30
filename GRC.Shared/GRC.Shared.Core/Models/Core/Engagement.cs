using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

using GRC.Shared.Core.Enums;
using GRC.Shared.Core.Models.Assignments;
using GRC.Shared.Core.Models.Financial;
using GRC.Shared.Core.Models.Lookups;

namespace GRC.Shared.Core.Models.Core
{
    /// <summary>
    /// Represents a client engagement.
    /// </summary>
    public class Engagement
    {
        public int Id { get; set; }
        public string EngagementId { get; set; } = string.Empty; // Business key from source files
        public string Description { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        public decimal? MarginPctBudget { get; set; }
        public decimal? MarginPctEtcp { get; set; }
        public DateTime? LastEtcDate { get; set; }
        public DateTime? ProposedNextEtcDate { get; set; }
        public string? StatusText { get; set; }
        public decimal OpeningValue { get; set; }
        public EngagementStatus Status { get; set; }
        public EngagementSource Source { get; set; } = EngagementSource.GrcProject;
        public decimal OpeningExpenses { get; set; }
        public decimal InitialHoursBudget { get; set; }
        public decimal EstimatedToCompleteHours { get; set; }
        public decimal ValueEtcp { get; set; }
        public decimal ExpensesEtcp { get; set; }
        public int? UnbilledRevenueDays { get; set; }

        public int? CustomerId { get; set; }
        public Customer? Customer { get; set; }

        public int? LastClosingPeriodId { get; set; }
        public ClosingPeriod? LastClosingPeriod { get; set; }
        
        [NotMapped]
        public string? LastClosingPeriodName { get; set; }

        public ICollection<EngagementPapd> EngagementPapds { get; } = new List<EngagementPapd>();
        public ICollection<EngagementManagerAssignment> ManagerAssignments { get; } = new List<EngagementManagerAssignment>();
        public ICollection<EngagementRankBudget> RankBudgets { get; } = new List<EngagementRankBudget>();
        public ICollection<FinancialEvolution> FinancialEvolutions { get; } = new List<FinancialEvolution>();
        public ICollection<EngagementFiscalYearRevenueAllocation> RevenueAllocations { get; } = new List<EngagementFiscalYearRevenueAllocation>();
        public ICollection<EngagementAdditionalSale> AdditionalSales { get; } = new List<EngagementAdditionalSale>();

        [NotMapped]
        public bool IsManualOnly => Source == EngagementSource.S4Project;

        [NotMapped]
        public string CustomerName => Customer?.Name ?? string.Empty;

        [NotMapped]
        public int? EtcpAgeDays
        {
            get
            {
                if (!LastEtcDate.HasValue)
                {
                    return null;
                }

                DateTime today = DateTime.UtcNow.Date;
                DateTime lastEtcDate = LastEtcDate.Value.Date;
                int age = (today - lastEtcDate).Days;
                return age < 0 ? 0 : age;
            }
        }

        [NotMapped]
        public decimal CurrentRevenueAllocation => ValueEtcp > 0m ? ValueEtcp : OpeningValue;

        [NotMapped]
        public decimal RevenueAllocationVariance { get; set; }

        [NotMapped]
        public bool RequiresRevenueAllocationReview { get; set; }

        [NotMapped]
        public decimal ValueToAllocate
        {
            get
            {
                return ValueEtcp > 0m
                    ? ValueEtcp
                    : OpeningValue > 0m
                        ? OpeningValue
                        : 0m;
            }
        }
    }
}
