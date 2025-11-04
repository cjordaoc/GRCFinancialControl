namespace GRCFinancialControl.Core.Models
{
    public record HoursAllocationCellUpdate(long BudgetId, decimal ConsumedHours, decimal? BudgetHours = null);
}
