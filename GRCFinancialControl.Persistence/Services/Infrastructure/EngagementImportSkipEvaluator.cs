using System;
using GRCFinancialControl.Core.Enums;
using GRCFinancialControl.Core.Models;

namespace GRCFinancialControl.Persistence.Services.Infrastructure
{
    /// <summary>
    /// Evaluates whether an engagement should be skipped during import based on source and status.
    /// </summary>
    internal static class EngagementImportSkipEvaluator
    {
        public static bool TryCreate(Engagement? engagement, out ImportSkipMetadata metadata)
        {
            if (engagement is null)
            {
                metadata = default;
                return false;
            }

            if (engagement.Source == EngagementSource.S4Project)
            {
                metadata = new ImportSkipMetadata(
                    "ManualOnly",
                    $"⚠ Values for S/4 Projects must be inserted manually. Data import was skipped for Engagement {engagement.EngagementId}.");
                return true;
            }

            if (engagement.Status == EngagementStatus.Closed)
            {
                metadata = new ImportSkipMetadata(
                    "ClosedEngagement",
                    $"⚠ Engagement {engagement.EngagementId} skipped – status is Closed.");
                return true;
            }

            metadata = default;
            return false;
        }
    }

    internal readonly struct ImportSkipMetadata
    {
        public ImportSkipMetadata(string reasonKey, string warningMessage)
        {
            ReasonKey = string.IsNullOrWhiteSpace(reasonKey)
                ? throw new ArgumentException("Reason key must be provided.", nameof(reasonKey))
                : reasonKey.Trim();
            WarningMessage = string.IsNullOrWhiteSpace(warningMessage)
                ? throw new ArgumentException("Warning message must be provided.", nameof(warningMessage))
                : warningMessage.Trim();
        }

        public string ReasonKey { get; }
        public string WarningMessage { get; }
    }
}
