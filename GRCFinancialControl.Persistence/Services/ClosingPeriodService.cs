using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Infrastructure;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GRCFinancialControl.Persistence.Services
{
    public class ClosingPeriodService : ContextFactoryCrudService<ClosingPeriod>, IClosingPeriodService
    {
        private const string CannotDeleteClosingPeriodMessage = "Cannot delete a closing period that is linked to imported margin data.";

        public ClosingPeriodService(IDbContextFactory<ApplicationDbContext> contextFactory)
            : base(contextFactory)
        {
        }

        protected override DbSet<ClosingPeriod> Set(ApplicationDbContext context) => context.ClosingPeriods;

        public Task<List<ClosingPeriod>> GetAllAsync() => GetAllInternalAsync(static query => query.AsNoTracking().OrderBy(cp => cp.PeriodStart));

        public Task AddAsync(ClosingPeriod period) => AddEntityAsync(period);

        public Task UpdateAsync(ClosingPeriod period) => UpdateEntityAsync(period);

        public async Task DeleteAsync(int id)
        {
            if (id <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(id), id, "Closing period identifier must be positive.");
            }

            await using var context = await CreateContextAsync();

            var period = await context.ClosingPeriods.FindAsync(id);
            if (period == null)
            {
                return;
            }

            var hasActuals = await context.ActualsEntries.AnyAsync(a => a.ClosingPeriodId == id);
            if (hasActuals)
            {
                throw new InvalidOperationException(CannotDeleteClosingPeriodMessage);
            }

            context.ClosingPeriods.Remove(period);
            await context.SaveChangesAsync();
        }

        public async Task DeleteDataAsync(int closingPeriodId)
        {
            if (closingPeriodId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(closingPeriodId), closingPeriodId, "Closing period identifier must be positive.");
            }

            await using var context = await CreateContextAsync();

            await context.ActualsEntries
                .Where(a => a.ClosingPeriodId == closingPeriodId)
                .ExecuteDeleteAsync();

            await context.PlannedAllocations
                .Where(p => p.ClosingPeriodId == closingPeriodId)
                .ExecuteDeleteAsync();
        }
    }
}
