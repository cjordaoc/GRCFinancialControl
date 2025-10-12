using System.Collections.Generic;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GRCFinancialControl.Persistence.Services
{
    public class PapdService : IPapdService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public PapdService(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<List<Papd>> GetAllAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Papds.ToListAsync();
        }

        public async Task AddAsync(Papd papd)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            await context.Papds.AddAsync(papd);
            await context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Papd papd)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            context.Papds.Update(papd);
            await context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var papd = await context.Papds.FindAsync(id);
            if (papd != null)
            {
                context.Papds.Remove(papd);
                await context.SaveChangesAsync();
            }
        }
    }
}