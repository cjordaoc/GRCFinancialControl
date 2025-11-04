using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Infrastructure;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GRCFinancialControl.Persistence.Services
{
    public class PapdAssignmentService : IPapdAssignmentService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public PapdAssignmentService(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<List<EngagementPapd>> GetByEngagementIdAsync(int engagementId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.EngagementPapds
                .AsNoTracking()
                .Where(a => a.EngagementId == engagementId)
                .ToListAsync();
        }

        public async Task<List<EngagementPapd>> GetByPapdIdAsync(int papdId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.EngagementPapds
                .AsNoTracking()
                .AsSplitQuery()
                .Include(a => a.Papd)
                .Include(a => a.Engagement)
                .Where(a => a.PapdId == papdId)
                .OrderBy(a => a.Engagement.EngagementId)
                .ThenBy(a => a.Engagement.Description)
                .ToListAsync();
        }

        public async Task DeleteAsync(int id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var existingAssignment = await context.EngagementPapds.FirstOrDefaultAsync(a => a.Id == id);
            if (existingAssignment is null)
            {
                return;
            }

            await EngagementMutationGuard.EnsureCanMutateAsync(
                context,
                existingAssignment.EngagementId,
                "Deleting PAPD assignments",
                allowManualSources: true);

            context.EngagementPapds.Remove(existingAssignment);
            await context.SaveChangesAsync();
        }

        public async Task UpdateAssignmentsForEngagementAsync(int engagementId, IEnumerable<int> papdIds)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            await EngagementMutationGuard.EnsureCanMutateAsync(
                context,
                engagementId,
                "Updating PAPD assignments",
                allowManualSources: true);

            var existingAssignments = await context.EngagementPapds
                .Where(a => a.EngagementId == engagementId)
                .ToListAsync();

            var existingPapdIds = existingAssignments.Select(a => a.PapdId).ToHashSet();
            var incomingPapdIds = papdIds.ToHashSet();

            var assignmentsToRemove = existingAssignments
                .Where(a => !incomingPapdIds.Contains(a.PapdId))
                .ToList();

            var papdIdsToAdd = incomingPapdIds
                .Where(id => !existingPapdIds.Contains(id))
                .ToList();

            if (assignmentsToRemove.Any())
            {
                context.EngagementPapds.RemoveRange(assignmentsToRemove);
            }

            if (papdIdsToAdd.Any())
            {
                var newAssignments = papdIdsToAdd.Select(papdId => new EngagementPapd
                {
                    EngagementId = engagementId,
                    PapdId = papdId
                });
                await context.EngagementPapds.AddRangeAsync(newAssignments);
            }

            if (assignmentsToRemove.Any() || papdIdsToAdd.Any())
            {
                await context.SaveChangesAsync();
            }
        }
    }
}
