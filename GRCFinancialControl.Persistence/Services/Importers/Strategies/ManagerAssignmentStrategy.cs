using System;
using System.Collections.Generic;
using System.Linq;
using GRCFinancialControl.Core.Enums;
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
        /// Cache of newly-created placeholder managers within current import session
        /// to prevent duplicate placeholders for the same GUI identifier.
        /// Key: GUI identifier, Value: Manager entity
        /// </summary>
        private static readonly Dictionary<string, Manager> _createdPlaceholders = new(StringComparer.OrdinalIgnoreCase);

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

            // Resolve GUI IDs to Manager IDs and create missing ones
            foreach (var gui in guiIds)
            {
                if (entityLookup.TryGetValue(gui, out var manager))
                {
                    desiredManagerIds.Add(manager.Id);
                }
                else if (_createdPlaceholders.TryGetValue(gui, out var placeholder))
                {
                    // Reuse previously-created placeholder for this GUI in current import session
                    desiredManagerIds.Add(placeholder.Id);
                }
                else
                {
                    // Check if Manager already exists in database by GUI code
                    var existingManager = context.Managers
                        .FirstOrDefault(m => m.EngagementManagerGui == gui);
                    
                    if (existingManager != null)
                    {
                        // Found in database, use it
                        _createdPlaceholders[gui] = existingManager;
                        if (entityLookup is IDictionary<string, Manager> mutableLookup)
                        {
                            mutableLookup[gui] = existingManager;
                        }
                        desiredManagerIds.Add(existingManager.Id);
                    }
                    else
                    {
                        // Create new Manager placeholder with the GUI identifier
                        // User can manually edit the details later
                        var newManager = new Manager
                        {
                            EngagementManagerGui = gui,
                            Name = gui,
                            Email = $"placeholder.{Guid.NewGuid().ToString().Substring(0, 8)}@tbd",
                            WindowsLogin = null,
                            Position = ManagerPosition.Manager
                        };
                        context.Managers.Add(newManager);
                        
                        // Save immediately to get database-generated Id
                        context.SaveChanges();

                        _createdPlaceholders[gui] = newManager;

                        // Keep lookup in sync so subsequent additions can resolve the entity without re-querying
                        if (entityLookup is IDictionary<string, Manager> mutableLookup)
                        {
                            mutableLookup[gui] = newManager;
                        }

                        desiredManagerIds.Add(newManager.Id);
                    }
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
                    var assignment = new EngagementManagerAssignment
                    {
                        EngagementId = engagement.Id,
                        ManagerId = managerId
                    };
                    engagement.ManagerAssignments.Add(assignment);
                    context.EngagementManagerAssignments.Add(assignment);
                    changeCount++;
                }
            }

            return changeCount;
        }

        /// <summary>
        /// Clears the placeholder cache after import completes.
        /// Should be called at the end of each import session.
        /// </summary>
        public static void ClearPlaceholderCache()
        {
            _createdPlaceholders.Clear();
        }
    }
}
