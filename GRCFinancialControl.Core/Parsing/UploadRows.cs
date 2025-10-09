using System;
using System.Collections.Generic;

namespace GRCFinancialControl.Parsing
{
    public sealed class PlanRow
    {
        public string EngagementId { get; set; } = string.Empty;
        public string EmployeeName { get; set; } = string.Empty;
        public string ResourceId { get; set; } = string.Empty;
        public string RawLevel { get; set; } = string.Empty;
        public string? NormalizedLevel { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public decimal PlannedHours { get; set; }
        public decimal? PlannedRate { get; set; }
        public Dictionary<DateOnly, decimal> WeeklyHours { get; } = new();
    }

    public sealed class EtcRow
    {
        public int ExcelRowNumber { get; set; }
        public string EngagementId { get; set; } = string.Empty;
        public string EmployeeName { get; set; } = string.Empty;
        public string EmployeeId { get; set; } = string.Empty;
        public string RawLevel { get; set; } = string.Empty;
        public string? NormalizedLevel { get; set; }
        public decimal HoursIncurred { get; set; }
        public decimal EtcRemaining { get; set; }
        public decimal? ProjectedMarginPercent { get; set; }
        public int? EtcAgeDays { get; set; }
        public int? RemainingWeeks { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public sealed class MarginDataRow
    {
        public int ExcelRowNumber { get; set; }
        public string EngagementId { get; set; } = string.Empty;
        public string? EngagementTitle { get; set; }
        public string? ClientName { get; set; }
        public string? EtcIndicatorTooltip { get; set; }
        public decimal? BudgetMarginPercent { get; set; }
        public decimal? EtcMarginPercent { get; set; }
        public decimal? MercuryProjectedMarginPercent { get; set; }
        public decimal? BudgetMarginValue { get; set; }
        public decimal? EtcMarginValue { get; set; }
        public decimal? MercuryProjectedMarginValue { get; set; }
        public decimal? MarginLossGainPercent { get; set; }
        public decimal? MarginLossGainValue { get; set; }
        public decimal? BillingOverrun { get; set; }
        public decimal? ExpensesOverrun { get; set; }
        public decimal? MarginCostOverrun { get; set; }
        public decimal? ActualMarginPercent { get; set; }
        public decimal? OpeningMargin { get; set; }
        public decimal? CurrentMargin { get; set; }
        public decimal? MarginValue { get; set; }
        public int? EtcAgeDays { get; set; }
        public int? RemainingWeeks { get; set; }
        public string? Status { get; set; }
        public int? EngagementCount { get; set; }
    }

    public sealed class WeeklyDeclarationRow
    {
        public string EngagementId { get; set; } = string.Empty;
        public DateOnly WeekStart { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public decimal DeclaredHours { get; set; }
    }

    public sealed class ChargeRow
    {
        public string EngagementId { get; set; } = string.Empty;
        public DateOnly ChargeDate { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public decimal Hours { get; set; }
        public decimal? CostAmount { get; set; }
    }

    public sealed class EtcParseResult : ExcelParseResult<EtcRow>
    {
        public EtcParseResult() : base("ETC Upload")
        {
        }
    }

    public sealed class MarginDataParseResult : ExcelParseResult<MarginDataRow>
    {
        public MarginDataParseResult() : base("Margin Data")
        {
        }
    }
}
