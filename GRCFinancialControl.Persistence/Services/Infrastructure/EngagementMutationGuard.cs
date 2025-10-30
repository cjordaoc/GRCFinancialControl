using System;
using System.Linq;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace GRCFinancialControl.Persistence.Services.Infrastructure
{
    internal static class EngagementMutationGuard
    {
        public static async Task EnsureCanMutateAsync(
            ApplicationDbContext context,
            int engagementId,
            string operationDescription,
            bool allowManualSources = false)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (string.IsNullOrWhiteSpace(operationDescription))
            {
                operationDescription = "This operation";
            }

            var engagement = await context.Engagements
                .AsNoTracking()
                .Select(e => new { e.Id, e.EngagementId, e.Source })
                .FirstOrDefaultAsync(e => e.Id == engagementId);

            if (engagement == null)
            {
                throw new InvalidOperationException(
                    $"Engagement with Id={engagementId} could not be found. {operationDescription} cannot continue.");
            }

            if (engagement.Source == EngagementSource.S4Project && !allowManualSources)
            {
                throw new InvalidOperationException(
                    $"Engagement '{engagement.EngagementId}' is sourced from S/4Project and must be managed manually. " +
                    $"{operationDescription} cannot modify manual-only engagements.");
            }
        }
    }
}
