using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using GRCFinancialControl.Core.Enums;

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

        public int? LastClosingPeriodId { get; set; }
        public ClosingPeriod? LastClosingPeriod { get; set; }

        public int? CustomerId { get; set; }
        public Customer? Customer { get; set; }

        public ICollection<EngagementPapd> EngagementPapds { get; set; } = [];
        public ICollection<EngagementManagerAssignment> ManagerAssignments { get; set; } = [];
        public ICollection<EngagementRankBudget> RankBudgets { get; set; } = [];
        public ICollection<FinancialEvolution> FinancialEvolutions { get; set; } = [];
        public ICollection<EngagementFiscalYearAllocation> Allocations { get; set; } = [];
        public ICollection<EngagementFiscalYearRevenueAllocation> RevenueAllocations { get; set; } = [];

        [NotMapped]
        public bool IsManualOnly => Source == EngagementSource.S4Project;

        [NotMapped]
        public decimal CurrentHoursAllocation => Allocations.Sum(a => a.PlannedHours);

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
        public decimal HoursToAllocate
        {
            get
            {
                return EstimatedToCompleteHours > 0m
                    ? EstimatedToCompleteHours
                    : InitialHoursBudget > 0m
                        ? InitialHoursBudget
                        : 0m;
            }
        }

        [NotMapped]
        public decimal AllocationVariance => CurrentHoursAllocation - HoursToAllocate;

        [NotMapped]
        public bool RequiresAllocationReview => Math.Abs(AllocationVariance) > 0.01m;

        [NotMapped]
        public decimal CurrentRevenueAllocation => RevenueAllocations.Sum(a => a.ToGoValue + a.ToDateValue);

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

        [NotMapped]
        public decimal RevenueAllocationVariance => CurrentRevenueAllocation - ValueToAllocate;

        [NotMapped]
        public bool RequiresRevenueAllocationReview => Math.Abs(RevenueAllocationVariance) > 0.01m;

        [NotMapped]
        public string LastClosingPeriodName => LastClosingPeriod?.Name ?? string.Empty;
    }
}
