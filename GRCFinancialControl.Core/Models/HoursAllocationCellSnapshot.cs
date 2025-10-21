namespace GRCFinancialControl.Core.Models
{
    public record HoursAllocationCellSnapshot(
        long? BudgetId,
        int FiscalYearId,
        decimal BudgetHours,
        decimal ConsumedHours,
        decimal RemainingHours,
        bool IsLocked);
}
