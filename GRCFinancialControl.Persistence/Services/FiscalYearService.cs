using System.Collections.Generic;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GRCFinancialControl.Persistence.Services
{
    public class FiscalYearService : IFiscalYearService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public FiscalYearService(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<List<FiscalYear>> GetAllAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Set<FiscalYear>().ToListAsync();
        }

        public async Task AddAsync(FiscalYear fiscalYear)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            await context.ClosingPeriods.AddAsync(fiscalYear);
            await context.SaveChangesAsync();
        }

        public async Task UpdateAsync(FiscalYear fiscalYear)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            context.ClosingPeriods.Update(fiscalYear);
            await context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var fiscalYear = await context.ClosingPeriods.FindAsync(id);
            if (fiscalYear != null)
            {
                context.ClosingPeriods.Remove(fiscalYear);
                await context.SaveChangesAsync();
            }
        }
    }
}