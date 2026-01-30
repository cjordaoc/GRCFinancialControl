namespace GRC.Shared.Core.Models.Allocations
{
    public record HoursAllocationCellUpdate(long BudgetId, decimal ConsumedHours, decimal? BudgetHours = null);
}
