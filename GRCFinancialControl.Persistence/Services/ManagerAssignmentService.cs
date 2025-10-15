using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Models;
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
                .Include(a => a.Manager)
                .Include(a => a.Engagement)
                .OrderBy(a => a.Engagement.EngagementId)
                .ThenBy(a => a.BeginDate)
                .ToListAsync();
        }

        public async Task<List<EngagementManagerAssignment>> GetByEngagementIdAsync(int engagementId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.EngagementManagerAssignments
                .Include(a => a.Manager)
                .Include(a => a.Engagement)
                .Where(a => a.EngagementId == engagementId)
                .OrderBy(a => a.BeginDate)
                .ThenBy(a => a.Manager.Name)
                .ToListAsync();
        }

        public async Task<EngagementManagerAssignment?> GetByIdAsync(int id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.EngagementManagerAssignments
                .Include(a => a.Manager)
                .Include(a => a.Engagement)
                .FirstOrDefaultAsync(a => a.Id == id);
        }

        public async Task AddAsync(EngagementManagerAssignment assignment)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
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

            existingAssignment.EngagementId = assignment.EngagementId;
            existingAssignment.ManagerId = assignment.ManagerId;
            existingAssignment.BeginDate = assignment.BeginDate;
            existingAssignment.EndDate = assignment.EndDate;

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

            context.EngagementManagerAssignments.Remove(existingAssignment);
            await context.SaveChangesAsync();
        }
    }
}
