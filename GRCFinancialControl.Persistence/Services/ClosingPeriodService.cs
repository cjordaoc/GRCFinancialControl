using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GRCFinancialControl.Persistence.Services
{
    public class ClosingPeriodService : IClosingPeriodService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public ClosingPeriodService(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<List<ClosingPeriod>> GetAllAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            return await context.Set<ClosingPeriod>().ToListAsync();
        }

        public async Task AddAsync(ClosingPeriod period)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            await context.ClosingPeriods.AddAsync(period);
            await context.SaveChangesAsync();
        }

        public async Task UpdateAsync(ClosingPeriod period)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            context.ClosingPeriods.Update(period);
            await context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var period = await context.ClosingPeriods.FindAsync(id);
            if (period == null)
            {
                return;
            }

            var hasActuals = await context.ActualsEntries.AnyAsync(a => a.ClosingPeriodId == id);
            if (hasActuals)
            {
                throw new InvalidOperationException("Cannot delete a closing period that is linked to imported margin data.");
            }

            context.ClosingPeriods.Remove(period);
            await context.SaveChangesAsync();
        }
    }
}
