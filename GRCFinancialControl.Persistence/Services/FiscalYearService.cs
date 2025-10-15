using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Infrastructure;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GRCFinancialControl.Persistence.Services
{
    public class FiscalYearService : ContextFactoryCrudService<FiscalYear>, IFiscalYearService
    {
        public FiscalYearService(IDbContextFactory<ApplicationDbContext> contextFactory)
            : base(contextFactory)
        {
        }

        protected override DbSet<FiscalYear> Set(ApplicationDbContext context) => context.FiscalYears;

        public Task<List<FiscalYear>> GetAllAsync() => GetAllInternalAsync(static query => query.AsNoTracking().OrderBy(fy => fy.StartDate));

        public Task AddAsync(FiscalYear fiscalYear) => AddEntityAsync(fiscalYear);

        public Task UpdateAsync(FiscalYear fiscalYear) => UpdateEntityAsync(fiscalYear);

        public Task DeleteAsync(int id) => DeleteEntityAsync(id);

        public async Task DeleteDataAsync(int fiscalYearId)
        {
            await using var context = await CreateContextAsync();

            var fiscalYear = await context.FiscalYears
                .AsNoTracking()
                .Where(f => f.Id == fiscalYearId)
                .Select(f => new { f.StartDate, f.EndDate })
                .FirstOrDefaultAsync();

            if (fiscalYear is null)
            {
                return;
            }

            await context.EngagementFiscalYearAllocations
                .Where(a => a.FiscalYearId == fiscalYearId)
                .ExecuteDeleteAsync();

            await context.ActualsEntries
                .Where(a => a.Date >= fiscalYear.StartDate && a.Date <= fiscalYear.EndDate)
                .ExecuteDeleteAsync();
        }
    }
}