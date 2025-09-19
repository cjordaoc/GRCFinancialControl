using System;

namespace GRCFinancialControl.Parsing
{
    public sealed class PlanRow
    {
        public string RawLevel { get; set; } = string.Empty;
        public decimal PlannedHours { get; set; }
        public decimal? PlannedRate { get; set; }
    }

    public sealed class EtcRow
    {
        public string EmployeeName { get; set; } = string.Empty;
        public string RawLevel { get; set; } = string.Empty;
        public decimal HoursIncurred { get; set; }
        public decimal EtcRemaining { get; set; }
    }

    public sealed class MarginDataRow
    {
        public int ExcelRowNumber { get; set; }
        public string EngagementId { get; set; } = string.Empty;
        public string? EngagementTitle { get; set; }
        public decimal? OpeningMargin { get; set; }
        public decimal? CurrentMargin { get; set; }
        public decimal? MarginValue { get; set; }
    }

    public sealed class WeeklyDeclarationRow
    {
        public DateOnly WeekStart { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public decimal DeclaredHours { get; set; }
    }

    public sealed class ChargeRow
    {
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
