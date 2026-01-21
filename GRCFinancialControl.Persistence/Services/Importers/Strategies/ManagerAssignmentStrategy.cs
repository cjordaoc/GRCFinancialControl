using System;
using System.Collections.Generic;
using System.Linq;
using GRCFinancialControl.Core.Models;

namespace GRCFinancialControl.Persistence.Services.Importers.Strategies
{
    /// <summary>
    /// Strategy for synchronizing EngagementManager assignments.
    /// Handles the logic of adding/removing manager relationships to engagements.
    /// </summary>
    public class ManagerAssignmentStrategy : IAssignmentStrategy<Manager, EngagementManagerAssignment>
    {
        /// <summary>
        /// Syncs manager assignments to desired state.
        /// </summary>
        /// <param name="context">The database context.</param>
        /// <param name="engagement">The engagement being updated.</param>
        /// <param name="guiIds">Collection of GUI identifiers for managers to assign.</param>
        /// <param name="entityLookup">Lookup dictionary mapping GUIDs to Manager entities.</param>
        /// <param name="missingGuis">Set to track manager GUIDs that could not be resolved.</param>
        /// <returns>Number of changes made (additions + removals).</returns>
        public int SyncAssignments(
            ApplicationDbContext context,
            Engagement engagement,
            IReadOnlyCollection<string> guiIds,
            IReadOnlyDictionary<string, Manager> entityLookup,
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

            var desiredManagerIds = new HashSet<int>();
            var changeCount = 0;

            // Resolve GUI IDs to Manager IDs and track missing ones
            foreach (var gui in guiIds)
            {
                if (entityLookup.TryGetValue(gui, out var manager))
                {
                    desiredManagerIds.Add(manager.Id);
                }
                else
                {
                    missingGuis.Add(gui);
                }
            }

            // Remove assignments not in desired set
            var currentAssignments = engagement.ManagerAssignments.ToList();
            foreach (var assignment in currentAssignments)
            {
                if (!desiredManagerIds.Contains(assignment.ManagerId))
                {
                    engagement.ManagerAssignments.Remove(assignment);
                    context.EngagementManagerAssignments.Remove(assignment);
                    changeCount++;
                }
            }

            // Add new assignments
            var currentManagerIds = engagement.ManagerAssignments
                .Select(em => em.ManagerId)
                .ToHashSet();

            foreach (var managerId in desiredManagerIds)
            {
                if (!currentManagerIds.Contains(managerId))
                {
                    var manager = entityLookup.Values.First(m => m.Id == managerId);
                    var assignment = new EngagementManagerAssignment
                    {
                        EngagementId = engagement.Id,
                        ManagerId = managerId,
                        Manager = manager
                    };
                    engagement.ManagerAssignments.Add(assignment);
                    context.EngagementManagerAssignments.Add(assignment);
                    changeCount++;
                }
            }

            return changeCount;
        }
    }
}
