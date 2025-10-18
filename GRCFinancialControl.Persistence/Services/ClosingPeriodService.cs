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

        protected override IQueryable<ClosingPeriod> BuildQuery(ApplicationDbContext context)
        {
            return base.BuildQuery(context)
                .Include(cp => cp.FiscalYear);
        }

        public Task<List<ClosingPeriod>> GetAllAsync() => GetAllInternalAsync(static query => query.AsNoTracking().OrderBy(cp => cp.PeriodStart));

        public async Task AddAsync(ClosingPeriod period)
        {
            ArgumentNullException.ThrowIfNull(period);

            if (period.FiscalYearId <= 0)
            {
                throw new InvalidOperationException("A fiscal year must be selected before adding a closing period.");
            }

            await using var context = await CreateContextAsync();
            await FiscalYearLockGuard.EnsureFiscalYearUnlockedAsync(context, period.FiscalYearId, "add a closing period");

            var newPeriod = new ClosingPeriod
            {
                Name = period.Name,
                FiscalYearId = period.FiscalYearId,
                PeriodStart = period.PeriodStart,
                PeriodEnd = period.PeriodEnd
            };

            await context.ClosingPeriods.AddAsync(newPeriod);
            await context.SaveChangesAsync();

            period.Id = newPeriod.Id;
        }

        public async Task UpdateAsync(ClosingPeriod period)
        {
            ArgumentNullException.ThrowIfNull(period);

            if (period.FiscalYearId <= 0)
            {
                throw new InvalidOperationException("A fiscal year must be selected before updating a closing period.");
            }

            await using var context = await CreateContextAsync();

            var existing = await context.ClosingPeriods
                .Include(cp => cp.FiscalYear)
                .FirstOrDefaultAsync(cp => cp.Id == period.Id);

            if (existing is null)
            {
                return;
            }

            await FiscalYearLockGuard.EnsureFiscalYearUnlockedAsync(context, existing.FiscalYearId, "update the closing period");

            if (existing.FiscalYearId != period.FiscalYearId)
            {
                await FiscalYearLockGuard.EnsureFiscalYearUnlockedAsync(context, period.FiscalYearId, "update the closing period");
            }

            context.Entry(existing).CurrentValues.SetValues(period);
            await context.SaveChangesAsync();
        }

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

            await FiscalYearLockGuard.EnsureFiscalYearUnlockedAsync(context, period.FiscalYearId, "delete the closing period");

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

            await FiscalYearLockGuard.EnsureClosingPeriodUnlockedAsync(context, closingPeriodId, "delete data for the closing period");

            await context.ActualsEntries
                .Where(a => a.ClosingPeriodId == closingPeriodId)
                .ExecuteDeleteAsync();

            await context.PlannedAllocations
                .Where(p => p.ClosingPeriodId == closingPeriodId)
                .ExecuteDeleteAsync();
        }
    }
}
