using System;
using GRCFinancialControl.Core.Models;

namespace GRCFinancialControl.Core.Extensions
{
    public static class EngagementRankBudgetExtensions
    {
        private const MidpointRounding RoundingStrategy = MidpointRounding.AwayFromZero;

        public static decimal CalculateIncurredHours(this EngagementRankBudget budget)
        {
            ArgumentNullException.ThrowIfNull(budget);

            var value = budget.BudgetHours + budget.AdditionalHours - budget.RemainingHours - budget.ConsumedHours;
            return Math.Round(value, 2, RoundingStrategy);
        }

        public static decimal NormalizeHours(decimal value)
        {
            return Math.Round(value, 2, RoundingStrategy);
        }

        public static void ApplyIncurredHours(this EngagementRankBudget budget, decimal incurredHours)
        {
            ArgumentNullException.ThrowIfNull(budget);

            var normalizedIncurred = NormalizeHours(incurredHours);
            var remaining = budget.BudgetHours + budget.AdditionalHours - (normalizedIncurred + budget.ConsumedHours);
            budget.RemainingHours = NormalizeHours(remaining);
        }

        public static void UpdateConsumedHours(this EngagementRankBudget budget, decimal consumedHours)
        {
            ArgumentNullException.ThrowIfNull(budget);

            var incurred = budget.CalculateIncurredHours();
            budget.ConsumedHours = NormalizeHours(consumedHours);
            var remaining = budget.BudgetHours + budget.AdditionalHours - (incurred + budget.ConsumedHours);
            budget.RemainingHours = NormalizeHours(remaining);
        }

        public static void UpdateAdditionalHours(this EngagementRankBudget budget, decimal additionalHours)
        {
            ArgumentNullException.ThrowIfNull(budget);

            var incurred = budget.CalculateIncurredHours();
            budget.AdditionalHours = NormalizeHours(additionalHours);
            var remaining = budget.BudgetHours + budget.AdditionalHours - (incurred + budget.ConsumedHours);
            budget.RemainingHours = NormalizeHours(remaining);
        }
    }
}
