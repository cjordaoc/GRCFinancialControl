using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Infrastructure;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GRCFinancialControl.Persistence.Services
{
    public class ManagerService : ContextFactoryCrudService<Manager>, IManagerService
    {
        public ManagerService(IDbContextFactory<ApplicationDbContext> contextFactory)
            : base(contextFactory)
        {
        }

        protected override DbSet<Manager> Set(ApplicationDbContext context) => context.Managers;

        public Task<List<Manager>> GetAllAsync() => GetAllInternalAsync(static query => query.AsNoTracking().OrderBy(m => m.Name));

        public Task AddAsync(Manager manager) => AddEntityAsync(manager);

        public Task UpdateAsync(Manager manager) => UpdateEntityAsync(manager);

        public Task DeleteAsync(int id) => DeleteEntityAsync(id);
    }
}
