using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Infrastructure;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GRCFinancialControl.Persistence.Services
{
    public class ManagerAssignmentService : IManagerAssignmentService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public ManagerAssignmentService(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<List<EngagementManagerAssignment>> GetAllAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.EngagementManagerAssignments
                .AsNoTracking()
                .AsSplitQuery()
                .Include(a => a.Manager)
                .Include(a => a.Engagement)
                .OrderBy(a => a.Engagement.EngagementId)
                .ThenBy(a => a.Manager.Name)
                .ToListAsync();
        }

        public async Task<List<EngagementManagerAssignment>> GetByEngagementIdAsync(int engagementId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.EngagementManagerAssignments
                .AsNoTracking()
                .AsSplitQuery()
                .Include(a => a.Manager)
                .Include(a => a.Engagement)
                .Where(a => a.EngagementId == engagementId)
                .OrderBy(a => a.Manager.Name)
                .ToListAsync();
        }

        public async Task<List<EngagementManagerAssignment>> GetByManagerIdAsync(int managerId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.EngagementManagerAssignments
                .AsNoTracking()
                .AsSplitQuery()
                .Include(a => a.Manager)
                .Include(a => a.Engagement)
                .Where(a => a.ManagerId == managerId)
                .OrderBy(a => a.Engagement.EngagementId)
                .ThenBy(a => a.Engagement.Description)
                .ToListAsync();
        }

        public async Task<EngagementManagerAssignment?> GetByIdAsync(int id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.EngagementManagerAssignments
                .AsNoTracking()
                .AsSplitQuery()
                .Include(a => a.Manager)
                .Include(a => a.Engagement)
                .FirstOrDefaultAsync(a => a.Id == id);
        }

        public async Task AddAsync(EngagementManagerAssignment assignment)
        {
            if (assignment.Id != 0)
            {
                await UpdateAsync(assignment);
                return;
            }

            await using var context = await _contextFactory.CreateDbContextAsync();

            await EngagementMutationGuard.EnsureCanMutateAsync(
                context,
                assignment.EngagementId,
                "Adding manager assignments",
                allowManualSources: true);

            await context.EngagementManagerAssignments.AddAsync(assignment);
            await context.SaveChangesAsync();
        }

        public async Task UpdateAsync(EngagementManagerAssignment assignment)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var existingAssignment = await context.EngagementManagerAssignments.FirstOrDefaultAsync(a => a.Id == assignment.Id);
            if (existingAssignment is null)
            {
                return;
            }

            await EngagementMutationGuard.EnsureCanMutateAsync(
                context,
                existingAssignment.EngagementId,
                "Updating manager assignments",
                allowManualSources: true);

            if (existingAssignment.EngagementId != assignment.EngagementId)
            {
                await EngagementMutationGuard.EnsureCanMutateAsync(
                    context,
                    assignment.EngagementId,
                    "Reassigning manager assignments",
                    allowManualSources: true);
            }

            existingAssignment.EngagementId = assignment.EngagementId;
            existingAssignment.ManagerId = assignment.ManagerId;

            await context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var existingAssignment = await context.EngagementManagerAssignments.FirstOrDefaultAsync(a => a.Id == id);
            if (existingAssignment is null)
            {
                return;
            }

            await EngagementMutationGuard.EnsureCanMutateAsync(
                context,
                existingAssignment.EngagementId,
                "Deleting manager assignments",
                allowManualSources: true);

            context.EngagementManagerAssignments.Remove(existingAssignment);
            await context.SaveChangesAsync();
        }
    }
}
