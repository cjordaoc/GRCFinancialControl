using System.Collections.Generic;
using GRCFinancialControl.Core.Models;

namespace GRCFinancialControl.Persistence.Services.Importers.Strategies
{
    /// <summary>
    /// Strategy pattern for synchronizing engagement assignments (Manager, PAPD, etc.).
    /// Enables consistent implementation of add/remove logic across different entity types.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being assigned (Manager, Papd, etc.)</typeparam>
    /// <typeparam name="TAssignment">The junction entity type (EngagementManagerAssignment, EngagementPapd, etc.)</typeparam>
    public interface IAssignmentStrategy<TEntity, TAssignment>
        where TEntity : class
        where TAssignment : class
    {
        /// <summary>
        /// Syncs assignments to desired state.
        /// Removes assignments not in desired set and adds new ones.
        /// </summary>
        /// <param name="context">The database context.</param>
        /// <param name="engagement">The engagement being updated.</param>
        /// <param name="guiIds">Collection of GUI identifiers to assign.</param>
        /// <param name="entityLookup">Lookup dictionary mapping GUIDs to entities.</param>
        /// <param name="missingGuis">Set to track GUI identifiers that could not be resolved.</param>
        /// <returns>Number of changes made (additions + removals).</returns>
        int SyncAssignments(
            ApplicationDbContext context,
            Engagement engagement,
            IReadOnlyCollection<string> guiIds,
            IReadOnlyDictionary<string, TEntity> entityLookup,
            ISet<string> missingGuis);
    }
}
