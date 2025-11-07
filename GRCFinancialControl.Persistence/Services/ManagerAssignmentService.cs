using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Infrastructure;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GRCFinancialControl.Persistence.Services
{
    /// <summary>
    /// Manages manager-to-engagement assignment relationships.
    /// </summary>
    public class ManagerAssignmentService : IManagerAssignmentService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public ManagerAssignmentService(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<List<EngagementManagerAssignment>> GetAllAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            return await context.EngagementManagerAssignments
                .AsNoTracking()
                .AsSplitQuery()
                .Include(a => a.Manager)
                .Include(a => a.Engagement)
                .OrderBy(a => a.Engagement.EngagementId)
                .ThenBy(a => a.Manager.Name)
                .ToListAsync().ConfigureAwait(false);
        }

        public async Task<List<EngagementManagerAssignment>> GetByEngagementIdAsync(int engagementId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            return await context.EngagementManagerAssignments
                .AsNoTracking()
                .AsSplitQuery()
                .Include(a => a.Manager)
                .Include(a => a.Engagement)
                .Where(a => a.EngagementId == engagementId)
                .OrderBy(a => a.Manager.Name)
                .ToListAsync().ConfigureAwait(false);
        }

        public async Task<List<EngagementManagerAssignment>> GetByManagerIdAsync(int managerId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            return await context.EngagementManagerAssignments
                .AsNoTracking()
                .AsSplitQuery()
                .Include(a => a.Manager)
                .Include(a => a.Engagement)
                .Where(a => a.ManagerId == managerId)
                .OrderBy(a => a.Engagement.EngagementId)
                .ThenBy(a => a.Engagement.Description)
                .ToListAsync().ConfigureAwait(false);
        }

        public async Task<EngagementManagerAssignment?> GetByIdAsync(int id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            return await context.EngagementManagerAssignments
                .AsNoTracking()
                .AsSplitQuery()
                .Include(a => a.Manager)
                .Include(a => a.Engagement)
                .FirstOrDefaultAsync(a => a.Id == id).ConfigureAwait(false);
        }

        public async Task AddAsync(EngagementManagerAssignment assignment)
        {
            if (assignment.Id != 0)
            {
                await UpdateAsync(assignment);
                return;
            }

            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);

            await EngagementMutationGuard.EnsureCanMutateAsync(
                context,
                assignment.EngagementId,
                "Adding manager assignments",
                allowManualSources: true).ConfigureAwait(false);

            await context.EngagementManagerAssignments.AddAsync(assignment).ConfigureAwait(false);
            await context.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task UpdateAsync(EngagementManagerAssignment assignment)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            var existingAssignment = await context.EngagementManagerAssignments.FirstOrDefaultAsync(a => a.Id == assignment.Id).ConfigureAwait(false);
            if (existingAssignment is null)
            {
                return;
            }

            await EngagementMutationGuard.EnsureCanMutateAsync(
                context,
                existingAssignment.EngagementId,
                "Updating manager assignments",
                allowManualSources: true).ConfigureAwait(false);

            if (existingAssignment.EngagementId != assignment.EngagementId)
            {
                await EngagementMutationGuard.EnsureCanMutateAsync(
                    context,
                    assignment.EngagementId,
                    "Reassigning manager assignments",
                    allowManualSources: true).ConfigureAwait(false);
            }

            existingAssignment.EngagementId = assignment.EngagementId;
            existingAssignment.ManagerId = assignment.ManagerId;

            await context.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task DeleteAsync(int id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            var existingAssignment = await context.EngagementManagerAssignments.FirstOrDefaultAsync(a => a.Id == id).ConfigureAwait(false);
            if (existingAssignment is null)
            {
                return;
            }

            await EngagementMutationGuard.EnsureCanMutateAsync(
                context,
                existingAssignment.EngagementId,
                "Deleting manager assignments",
                allowManualSources: true).ConfigureAwait(false);

            context.EngagementManagerAssignments.Remove(existingAssignment);
            await context.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task UpdateAssignmentsForEngagementAsync(int engagementId, IEnumerable<int> managerIds)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);

            await EngagementMutationGuard.EnsureCanMutateAsync(
                context,
                engagementId,
                "Updating manager assignments",
                allowManualSources: true).ConfigureAwait(false);

            var existingAssignments = await context.EngagementManagerAssignments
                .Where(a => a.EngagementId == engagementId)
                .ToListAsync().ConfigureAwait(false);

            var existingManagerIds = existingAssignments.Select(a => a.ManagerId).ToHashSet();
            var incomingManagerIds = managerIds.ToHashSet();

            var assignmentsToRemove = existingAssignments
                .Where(a => !incomingManagerIds.Contains(a.ManagerId))
                .ToList();

            var managerIdsToAdd = incomingManagerIds
                .Where(id => !existingManagerIds.Contains(id))
                .ToList();

            if (assignmentsToRemove.Any())
            {
                context.EngagementManagerAssignments.RemoveRange(assignmentsToRemove);
            }

            if (managerIdsToAdd.Any())
            {
                var newAssignments = managerIdsToAdd.Select(managerId => new EngagementManagerAssignment
                {
                    EngagementId = engagementId,
                    ManagerId = managerId
                });
                await context.EngagementManagerAssignments.AddRangeAsync(newAssignments).ConfigureAwait(false);
            }

            if (assignmentsToRemove.Any() || managerIdsToAdd.Any())
            {
                await context.SaveChangesAsync().ConfigureAwait(false);
            }
        }
    }
}
