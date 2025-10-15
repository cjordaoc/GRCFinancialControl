using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Infrastructure;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GRCFinancialControl.Persistence.Services
{
    public class PapdService : ContextFactoryCrudService<Papd>, IPapdService
    {
        public PapdService(IDbContextFactory<ApplicationDbContext> contextFactory)
            : base(contextFactory)
        {
        }

        protected override DbSet<Papd> Set(ApplicationDbContext context) => context.Papds;

        public Task<List<Papd>> GetAllAsync() => GetAllInternalAsync(static query => query.AsNoTracking().OrderBy(p => p.Name));

        public Task AddAsync(Papd papd) => AddEntityAsync(papd);

        public Task UpdateAsync(Papd papd) => UpdateEntityAsync(papd);

        public Task DeleteAsync(int id) => DeleteEntityAsync(id);

        public async Task DeleteDataAsync(int papdId)
        {
            await using var context = await CreateContextAsync();

            await context.ActualsEntries
                .Where(a => a.PapdId == papdId)
                .ExecuteDeleteAsync();

            await context.EngagementPapds
                .Where(ep => ep.PapdId == papdId)
                .ExecuteDeleteAsync();
        }
    }
}