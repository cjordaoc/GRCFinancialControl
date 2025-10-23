namespace GRCFinancialControl.Avalonia.Messages
{
    public sealed record ApplicationParametersChangedMessage(int? FiscalYearId, int? ClosingPeriodId);
}
