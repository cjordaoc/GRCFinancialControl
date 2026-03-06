using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using GRC.Shared.Core.Models.Core;
using GRC.Shared.Core.Models.Financial;
using GRC.Shared.Core.Models.Allocations;

using GRC.Shared.Core.Models.Assignments;
using GRC.Shared.Core.Models.Lookups;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Infrastructure;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GRCFinancialControl.Persistence.Services
{
    /// <summary>
    /// Manages PAPD-to-engagement assignment relationships.
    /// </summary>
    public class PapdAssignmentService : IPapdAssignmentService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public PapdAssignmentService(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        [RequiresUnreferencedCode("EF Core model building and migrations are not fully compatible with trimming.")]
        public async Task<List<EngagementPapd>> GetByEngagementIdAsync(int engagementId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            return await context.EngagementPapds
                .AsNoTracking()
                .Where(a => a.EngagementId == engagementId)
                .ToListAsync().ConfigureAwait(false);
        }

        [RequiresUnreferencedCode("EF Core model building and migrations are not fully compatible with trimming.")]
        public async Task<List<EngagementPapd>> GetByPapdIdAsync(int papdId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            return await context.EngagementPapds
                .AsNoTracking()
                .Where(a => a.PapdId == papdId)
                .Include(a => a.Papd)
                .Include(a => a.Engagement)
                    .ThenInclude(e => e.Customer)
                .AsSplitQuery()
                .OrderBy(a => a.Engagement.EngagementId)
                .ThenBy(a => a.Engagement.Description)
                .ToListAsync().ConfigureAwait(false);
        }

        [RequiresUnreferencedCode("EF Core model building and migrations are not fully compatible with trimming.")]
        public async Task DeleteAsync(int id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            var existingAssignment = await context.EngagementPapds.FirstOrDefaultAsync(a => a.Id == id).ConfigureAwait(false);
            if (existingAssignment is null)
            {
                return;
            }

            await EngagementMutationGuard.EnsureCanMutateAsync(
                context,
                existingAssignment.EngagementId,
                "Deleting PAPD assignments",
                allowManualSources: true).ConfigureAwait(false);

            context.EngagementPapds.Remove(existingAssignment);
            await context.SaveChangesAsync().ConfigureAwait(false);
        }

        [RequiresUnreferencedCode("EF Core model building and migrations are not fully compatible with trimming.")]
        public async Task UpdateAssignmentsForEngagementAsync(int engagementId, IEnumerable<int> papdIds)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);

            await EngagementMutationGuard.EnsureCanMutateAsync(
                context,
                engagementId,
                "Updating PAPD assignments",
                allowManualSources: true).ConfigureAwait(false);

            var existingAssignments = await context.EngagementPapds
                .Where(a => a.EngagementId == engagementId)
                .ToListAsync().ConfigureAwait(false);

            var incomingPapdIds = papdIds as HashSet<int> ?? papdIds.ToHashSet();
            
            if (IsNoChangeRequired(existingAssignments, incomingPapdIds))
            {
                return;
            }

            var diff = CalculateAssignmentDifferences(existingAssignments, incomingPapdIds);

            ApplyAssignmentChanges(context, engagementId, diff);

            if (diff.HasChanges)
            {
                await context.SaveChangesAsync().ConfigureAwait(false);
            }
        }

        private static bool IsNoChangeRequired(List<EngagementPapd> existingAssignments, HashSet<int> incomingPapdIds)
        {
            return existingAssignments.Count == 0 && incomingPapdIds.Count == 0;
        }

        private static AssignmentDifferences CalculateAssignmentDifferences(
            List<EngagementPapd> existingAssignments, 
            HashSet<int> incomingPapdIds)
        {
            var existingPapdIds = new HashSet<int>(existingAssignments.Count);
            var assignmentsToRemove = new List<EngagementPapd>(existingAssignments.Count);
            
            foreach (var assignment in existingAssignments)
            {
                existingPapdIds.Add(assignment.PapdId);
                if (!incomingPapdIds.Contains(assignment.PapdId))
                {
                    assignmentsToRemove.Add(assignment);
                }
            }

            var papdIdsToAdd = new List<int>();
            foreach (var papdId in incomingPapdIds)
            {
                if (!existingPapdIds.Contains(papdId))
                {
                    papdIdsToAdd.Add(papdId);
                }
            }

            return new AssignmentDifferences(assignmentsToRemove, papdIdsToAdd);
        }

        private static void ApplyAssignmentChanges(
            ApplicationDbContext context, 
            int engagementId, 
            AssignmentDifferences diff)
        {
            if (diff.AssignmentsToRemove.Count > 0)
            {
                context.EngagementPapds.RemoveRange(diff.AssignmentsToRemove);
            }

            foreach (var papdId in diff.PapdIdsToAdd)
            {
                context.EngagementPapds.Add(CreateAssignment(engagementId, papdId));
            }
        }

        private static EngagementPapd CreateAssignment(int engagementId, int papdId)
        {
            return new EngagementPapd
            {
                EngagementId = engagementId,
                PapdId = papdId
            };
        }

        private readonly struct AssignmentDifferences
        {
            public AssignmentDifferences(List<EngagementPapd> assignmentsToRemove, List<int> papdIdsToAdd)
            {
                AssignmentsToRemove = assignmentsToRemove;
                PapdIdsToAdd = papdIdsToAdd;
            }

            public List<EngagementPapd> AssignmentsToRemove { get; }
            public List<int> PapdIdsToAdd { get; }
            public bool HasChanges => AssignmentsToRemove.Count > 0 || PapdIdsToAdd.Count > 0;
        }
    }
}

