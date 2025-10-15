using System;
using System.Collections.Generic;
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
        public decimal EtcpHours { get; set; }
        public decimal ValueEtcp { get; set; }
        public decimal ExpensesEtcp { get; set; }
        public string? LastClosingPeriodId { get; set; }

        public int? CustomerId { get; set; }
        public Customer? Customer { get; set; }

        public ICollection<EngagementPapd> EngagementPapds { get; set; } = new List<EngagementPapd>();
        public ICollection<EngagementManagerAssignment> ManagerAssignments { get; set; } = new List<EngagementManagerAssignment>();
        public ICollection<EngagementRankBudget> RankBudgets { get; set; } = new List<EngagementRankBudget>();
        public ICollection<FinancialEvolution> FinancialEvolutions { get; set; } = new List<FinancialEvolution>();
        public ICollection<EngagementFiscalYearAllocation> Allocations { get; set; } = new List<EngagementFiscalYearAllocation>();
        public ICollection<EngagementFiscalYearRevenueAllocation> RevenueAllocations { get; set; } = new List<EngagementFiscalYearRevenueAllocation>();
        [NotMapped]
        public double CurrentHoursAllocation => Allocations.Sum(a => a.PlannedHours);

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

                var today = DateTime.UtcNow.Date;
                var lastEtcDate = LastEtcDate.Value.Date;
                var age = (today - lastEtcDate).Days;
                return age < 0 ? 0 : age;
            }
        }

        [NotMapped]
        public double HoursToAllocate
        {
            get
            {
                if (EtcpHours > 0)
                {
                    return (double)EtcpHours;
                }

                if (InitialHoursBudget > 0)
                {
                    return (double)InitialHoursBudget;
                }

                return 0d;
            }
        }

        [NotMapped]
        public double AllocationVariance => CurrentHoursAllocation - HoursToAllocate;

        [NotMapped]
        public bool RequiresAllocationReview => Math.Abs(AllocationVariance) > 0.01d;

        [NotMapped]
        public double CurrentRevenueAllocation => RevenueAllocations.Sum(a => (double)a.PlannedValue);

        [NotMapped]
        public double ValueToAllocate
        {
            get
            {
                if (ValueEtcp > 0)
                {
                    return (double)ValueEtcp;
                }

                if (OpeningValue > 0)
                {
                    return (double)OpeningValue;
                }

                return 0d;
            }
        }

        [NotMapped]
        public double RevenueAllocationVariance => CurrentRevenueAllocation - ValueToAllocate;

        [NotMapped]
        public bool RequiresRevenueAllocationReview => Math.Abs(RevenueAllocationVariance) > 0.01d;
    }
}
