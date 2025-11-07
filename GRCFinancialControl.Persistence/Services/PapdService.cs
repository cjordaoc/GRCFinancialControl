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
    /// Manages PAPD (Partner/Principal) entities and engagement assignments.
    /// </summary>
    public class PapdService : ContextFactoryCrudService<Papd>, IPapdService
    {
        public PapdService(IDbContextFactory<ApplicationDbContext> contextFactory)
            : base(contextFactory)
        {
        }

        protected override DbSet<Papd> Set(ApplicationDbContext context) => context.Papds;

        public Task<List<Papd>> GetAllAsync() => GetAllInternalAsync(static query => query.AsNoTracking().OrderBy(p => p.Name));

        public Task<Papd?> GetByIdAsync(int id) =>
            GetSingleInternalAsync(
                query => query
                    .Include(p => p.EngagementPapds)
                    .Where(p => p.Id == id));

        public Task AddAsync(Papd papd) => AddEntityAsync(papd);

        public Task UpdateAsync(Papd papd) => UpdateEntityAsync(papd);

        public Task DeleteAsync(int id) => DeleteEntityAsync(id);

        public async Task DeleteDataAsync(int papdId)
        {
            await using var context = await CreateContextAsync().ConfigureAwait(false);

            await context.ActualsEntries
                .Where(a => a.PapdId == papdId)
                .ExecuteDeleteAsync().ConfigureAwait(false);

            await context.EngagementPapds
                .Where(ep => ep.PapdId == papdId)
                .ExecuteDeleteAsync().ConfigureAwait(false);
        }

        public async Task AssignEngagementsAsync(int papdId, List<int> engagementIds)
        {
            await using var context = await CreateContextAsync().ConfigureAwait(false);
            var papd = await context.Papds
                .SingleOrDefaultAsync(p => p.Id == papdId).ConfigureAwait(false);

            if (papd is null)
            {
                return;
            }

            var currentAssignments = await context.EngagementPapds
                .Where(ep => ep.PapdId == papdId)
                .ToListAsync().ConfigureAwait(false);

            var currentEngagementIds = currentAssignments.Select(a => a.EngagementId).ToList();
            var idsToAdd = engagementIds.Except(currentEngagementIds).ToList();
            var assignmentsToRemove = currentAssignments.Where(a => !engagementIds.Contains(a.EngagementId)).ToList();

            if (assignmentsToRemove.Any())
            {
                context.EngagementPapds.RemoveRange(assignmentsToRemove);
            }

            if (idsToAdd.Any())
            {
                foreach (var engagementId in idsToAdd)
                {
                    context.EngagementPapds.Add(new EngagementPapd
                    {
                        EngagementId = engagementId,
                        PapdId = papdId
                    });
                }
            }
            await context.SaveChangesAsync().ConfigureAwait(false);
        }
    }
}
