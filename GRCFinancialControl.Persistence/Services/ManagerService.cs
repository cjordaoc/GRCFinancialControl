using System.Collections.Generic;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GRCFinancialControl.Persistence.Services
{
    public class ManagerService : IManagerService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public ManagerService(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<List<Manager>> GetAllAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Managers.AsNoTracking().ToListAsync();
        }

        public async Task AddAsync(Manager manager)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            await context.Managers.AddAsync(manager);
            await context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Manager manager)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            context.Managers.Update(manager);
            await context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var manager = await context.Managers.FindAsync(id);
            if (manager is null)
            {
                return;
            }

            context.Managers.Remove(manager);
            await context.SaveChangesAsync();
        }
    }
}
