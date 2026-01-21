using System;
using System.Collections.Generic;
using System.Linq;
using GRCFinancialControl.Core.Models;

namespace GRCFinancialControl.Persistence.Services.Importers.Strategies
{
    /// <summary>
    /// Strategy for synchronizing EngagementPapd assignments.
    /// Handles the logic of adding/removing PAPD (Project Additional Professional Developer) relationships to engagements.
    /// </summary>
    public class PapdAssignmentStrategy : IAssignmentStrategy<Papd, EngagementPapd>
    {
        /// <summary>
        /// Syncs PAPD assignments to desired state.
        /// </summary>
        /// <param name="context">The database context.</param>
        /// <param name="engagement">The engagement being updated.</param>
        /// <param name="guiIds">Collection of GUI identifiers for PAPDs to assign.</param>
        /// <param name="entityLookup">Lookup dictionary mapping GUIDs to Papd entities.</param>
        /// <param name="missingGuis">Set to track PAPD GUIDs that could not be resolved.</param>
        /// <returns>Number of changes made (additions + removals).</returns>
        public int SyncAssignments(
            ApplicationDbContext context,
            Engagement engagement,
            IReadOnlyCollection<string> guiIds,
            IReadOnlyDictionary<string, Papd> entityLookup,
            ISet<string> missingGuis)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (engagement == null)
            {
                throw new ArgumentNullException(nameof(engagement));
            }

            if (guiIds == null)
            {
                throw new ArgumentNullException(nameof(guiIds));
            }

            if (entityLookup == null)
            {
                throw new ArgumentNullException(nameof(entityLookup));
            }

            if (missingGuis == null)
            {
                throw new ArgumentNullException(nameof(missingGuis));
            }

            var desiredPapdIds = new HashSet<int>();
            var changeCount = 0;

            // Resolve GUI IDs to PAPD IDs and track missing ones
            foreach (var gui in guiIds)
            {
                if (entityLookup.TryGetValue(gui, out var papd))
                {
                    desiredPapdIds.Add(papd.Id);
                }
                else
                {
                    missingGuis.Add(gui);
                }
            }

            // Remove assignments not in desired set
            var currentAssignments = engagement.EngagementPapds.ToList();
            foreach (var assignment in currentAssignments)
            {
                if (!desiredPapdIds.Contains(assignment.PapdId))
                {
                    engagement.EngagementPapds.Remove(assignment);
                    context.EngagementPapds.Remove(assignment);
                    changeCount++;
                }
            }

            // Add new assignments
            var currentPapdIds = engagement.EngagementPapds
                .Select(ep => ep.PapdId)
                .ToHashSet();

            foreach (var papdId in desiredPapdIds)
            {
                if (!currentPapdIds.Contains(papdId))
                {
                    var papd = entityLookup.Values.First(p => p.Id == papdId);
                    var assignment = new EngagementPapd
                    {
                        EngagementId = engagement.Id,
                        PapdId = papdId,
                        Papd = papd
                    };
                    engagement.EngagementPapds.Add(assignment);
                    context.EngagementPapds.Add(assignment);
                    changeCount++;
                }
            }

            return changeCount;
        }
    }
}
