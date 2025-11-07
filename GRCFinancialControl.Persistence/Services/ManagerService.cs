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
    /// Manages manager entities and engagement assignments.
    /// </summary>
    public class ManagerService : ContextFactoryCrudService<Manager>, IManagerService
    {
        public ManagerService(IDbContextFactory<ApplicationDbContext> contextFactory)
            : base(contextFactory)
        {
        }

        protected override DbSet<Manager> Set(ApplicationDbContext context) => context.Managers;

        public Task<List<Manager>> GetAllAsync() => GetAllInternalAsync(static query => query.AsNoTracking().OrderBy(m => m.Name));

        public Task<Manager?> GetByIdAsync(int id) =>
            GetSingleInternalAsync(
                query => query
                    .Include(m => m.EngagementAssignments)
                    .ThenInclude(e => e.Engagement)
                    .Where(m => m.Id == id));

        public Task AddAsync(Manager manager) => AddEntityAsync(manager);

        public Task UpdateAsync(Manager manager) => UpdateEntityAsync(manager);

        public Task DeleteAsync(int id) => DeleteEntityAsync(id);

        public async Task AssignEngagementsAsync(int managerId, List<int> engagementIds)
        {
            await using var context = await CreateContextAsync().ConfigureAwait(false);
            var manager = await context.Managers
                .Include(m => m.EngagementAssignments)
                .SingleOrDefaultAsync(m => m.Id == managerId).ConfigureAwait(false);

            if (manager is null)
            {
                return;
            }

            var currentEngagementIds = manager.EngagementAssignments.Select(e => e.EngagementId).ToList();
            var idsToAdd = engagementIds.Except(currentEngagementIds).ToList();
            var assignmentsToRemove = manager.EngagementAssignments.Where(e => !engagementIds.Contains(e.EngagementId)).ToList();

            if (assignmentsToRemove.Any())
            {
                context.EngagementManagerAssignments.RemoveRange(assignmentsToRemove);
            }

            if (idsToAdd.Any())
            {
                foreach (var engagementId in idsToAdd)
                {
                    manager.EngagementAssignments.Add(new EngagementManagerAssignment
                    {
                        EngagementId = engagementId,
                        ManagerId = managerId
                    });
                }
            }
            await context.SaveChangesAsync().ConfigureAwait(false);
        }
    }
}
