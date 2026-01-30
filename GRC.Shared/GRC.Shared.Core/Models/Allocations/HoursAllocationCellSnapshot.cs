namespace GRC.Shared.Core.Models.Allocations
{
    public record HoursAllocationCellSnapshot(
        long? BudgetId,
        int FiscalYearId,
        decimal BudgetHours,
        decimal ConsumedHours,
        decimal RemainingHours,
        bool IsLocked);
}
