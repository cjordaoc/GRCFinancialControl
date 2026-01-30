using System;
using System.Collections.Generic;
using System.Linq;
using GRC.Shared.Core.Enums;
using GRC.Shared.Core.Models.Core;
using GRC.Shared.Core.Models.Financial;
using GRC.Shared.Core.Models.Allocations;

using GRC.Shared.Core.Models.Assignments;
using GRC.Shared.Core.Models.Lookups;
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
        /// Cache of newly-created placeholder papds within current import session
        /// to prevent duplicate placeholders for the same GUI identifier.
        /// Key: GUI identifier, Value: Papd entity
        /// </summary>
        private static readonly Dictionary<string, Papd> _createdPlaceholders = new(StringComparer.OrdinalIgnoreCase);

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

            // Resolve GUI IDs to PAPD IDs and create missing ones
            foreach (var gui in guiIds)
            {
                if (entityLookup.TryGetValue(gui, out var papd))
                {
                    desiredPapdIds.Add(papd.Id);
                }
                else if (_createdPlaceholders.TryGetValue(gui, out var placeholder))
                {
                    // Reuse previously-created placeholder for this GUI in current import session
                    desiredPapdIds.Add(placeholder.Id);
                }
                else
                {
                    // Check if Papd already exists in database by GUI code
                    var existingPapd = context.Papds
                        .FirstOrDefault(p => p.EngagementPapdGui == gui);
                    
                    if (existingPapd != null)
                    {
                        // Found in database, use it
                        _createdPlaceholders[gui] = existingPapd;
                        if (entityLookup is IDictionary<string, Papd> mutableLookup)
                        {
                            mutableLookup[gui] = existingPapd;
                        }
                        desiredPapdIds.Add(existingPapd.Id);
                    }
                    else
                    {
                        // Create new Papd placeholder with the GUI identifier
                        // User can manually edit the details later
                        var newPapd = new Papd
                        {
                            EngagementPapdGui = gui,
                            Name = gui,
                            WindowsLogin = null,
                            Level = PapdLevel.AssociatePartner
                        };
                        context.Papds.Add(newPapd);
                        
                        // Save immediately to get database-generated Id
                        context.SaveChanges();

                        _createdPlaceholders[gui] = newPapd;

                        // Keep lookup in sync so subsequent additions can resolve the entity without re-querying
                        if (entityLookup is IDictionary<string, Papd> mutableLookup)
                        {
                            mutableLookup[gui] = newPapd;
                        }

                        desiredPapdIds.Add(newPapd.Id);
                    }
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
                    var assignment = new EngagementPapd
                    {
                        EngagementId = engagement.Id,
                        PapdId = papdId
                    };
                    engagement.EngagementPapds.Add(assignment);
                    context.EngagementPapds.Add(assignment);
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
