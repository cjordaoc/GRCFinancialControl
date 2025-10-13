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
            await context.FiscalYears.AddAsync(fiscalYear);
            await context.SaveChangesAsync();
        }

        public async Task UpdateAsync(FiscalYear fiscalYear)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            context.FiscalYears.Update(fiscalYear);
            await context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var fiscalYear = await context.FiscalYears.FindAsync(id);
            if (fiscalYear != null)
            {
                context.FiscalYears.Remove(fiscalYear);
                await context.SaveChangesAsync();
            }
        }

        public async Task DeleteDataAsync(int fiscalYearId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var fiscalYear = await context.FiscalYears.FindAsync(fiscalYearId);
            if (fiscalYear == null) return;

            var allocationsToDelete = await context.EngagementFiscalYearAllocations
                .Where(a => a.FiscalYearId == fiscalYearId)
                .ToListAsync();

            if (allocationsToDelete.Any())
            {
                context.EngagementFiscalYearAllocations.RemoveRange(allocationsToDelete);
            }

            var actualsToDelete = await context.ActualsEntries
                .Where(a => a.Date >= fiscalYear.StartDate && a.Date <= fiscalYear.EndDate)
                .ToListAsync();

            if (actualsToDelete.Any())
            {
                context.ActualsEntries.RemoveRange(actualsToDelete);
            }

            await context.SaveChangesAsync();
        }
    }
}