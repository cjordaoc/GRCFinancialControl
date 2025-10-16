using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Infrastructure;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GRCFinancialControl.Persistence.Services
{
    public class FiscalYearService : ContextFactoryCrudService<FiscalYear>, IFiscalYearService
    {
        private readonly ILogger<FiscalYearService> _logger;

        public FiscalYearService(IDbContextFactory<ApplicationDbContext> contextFactory,
            ILogger<FiscalYearService> logger)
            : base(contextFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override DbSet<FiscalYear> Set(ApplicationDbContext context) => context.FiscalYears;

        public Task<List<FiscalYear>> GetAllAsync() => GetAllInternalAsync(static query => query.AsNoTracking().OrderBy(fy => fy.StartDate));

        public Task AddAsync(FiscalYear fiscalYear) => AddEntityAsync(fiscalYear);

        public async Task UpdateAsync(FiscalYear fiscalYear)
        {
            await using var context = await CreateContextAsync();

            var existing = await context.FiscalYears.FindAsync(fiscalYear.Id);
            if (existing is null)
            {
                throw new InvalidOperationException($"Fiscal year with Id={fiscalYear.Id} was not found.");
            }

            if (existing.IsLocked)
            {
                throw new InvalidOperationException($"Fiscal year '{existing.Name}' is locked and cannot be modified. Unlock it before making changes.");
            }

            context.Entry(existing).CurrentValues.SetValues(fiscalYear);
            await context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            await using var context = await CreateContextAsync();

            var fiscalYear = await context.FiscalYears.FindAsync(id);
            if (fiscalYear is null)
            {
                return;
            }

            if (fiscalYear.IsLocked)
            {
                throw new InvalidOperationException($"Fiscal year '{fiscalYear.Name}' is locked and cannot be deleted. Unlock it before removing it.");
            }

            context.FiscalYears.Remove(fiscalYear);
            await context.SaveChangesAsync();
        }

        public async Task DeleteDataAsync(int fiscalYearId)
        {
            await using var context = await CreateContextAsync();

            await FiscalYearLockGuard.EnsureFiscalYearUnlockedAsync(context, fiscalYearId, "delete data for the fiscal year");

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

        public async Task<DateTime?> LockAsync(int fiscalYearId, string lockedBy)
        {
            await using var context = await CreateContextAsync();

            var fiscalYear = await context.FiscalYears.FindAsync(fiscalYearId);
            if (fiscalYear is null)
            {
                return null;
            }

            if (fiscalYear.IsLocked)
            {
                return fiscalYear.LockedAt;
            }

            var actor = string.IsNullOrWhiteSpace(lockedBy) ? "Unknown" : lockedBy;
            var lockedAt = DateTime.UtcNow;

            fiscalYear.IsLocked = true;
            fiscalYear.LockedAt = lockedAt;
            fiscalYear.LockedBy = actor;

            await context.SaveChangesAsync();

            _logger.LogInformation(
                "Fiscal year {FiscalYear} locked at {Timestamp} by {User}.",
                fiscalYear.Name,
                lockedAt,
                actor);

            return lockedAt;
        }

        public async Task<DateTime?> UnlockAsync(int fiscalYearId, string unlockedBy)
        {
            await using var context = await CreateContextAsync();

            var fiscalYear = await context.FiscalYears.FindAsync(fiscalYearId);
            if (fiscalYear is null)
            {
                return null;
            }

            if (!fiscalYear.IsLocked)
            {
                return null;
            }

            var actor = string.IsNullOrWhiteSpace(unlockedBy) ? "Unknown" : unlockedBy;
            var unlockedAt = DateTime.UtcNow;

            fiscalYear.IsLocked = false;
            fiscalYear.LockedAt = null;
            fiscalYear.LockedBy = null;

            await context.SaveChangesAsync();

            _logger.LogInformation(
                "Fiscal year {FiscalYear} unlocked at {Timestamp} by {User}.",
                fiscalYear.Name,
                unlockedAt,
                actor);

            return unlockedAt;
        }

        public async Task<FiscalYearCloseResult> CloseAsync(int fiscalYearId, string closedBy)
        {
            await using var context = await CreateContextAsync();

            var fiscalYears = await context.FiscalYears
                .OrderBy(fy => fy.StartDate)
                .ToListAsync();

            var fiscalYear = fiscalYears.FirstOrDefault(fy => fy.Id == fiscalYearId);
            if (fiscalYear is null)
            {
                throw new InvalidOperationException($"Fiscal year with Id={fiscalYearId} was not found.");
            }

            var actor = string.IsNullOrWhiteSpace(closedBy) ? "Unknown" : closedBy;
            DateTime lockedAt;

            if (!fiscalYear.IsLocked)
            {
                fiscalYear.IsLocked = true;
                lockedAt = DateTime.UtcNow;
                fiscalYear.LockedAt = lockedAt;
                fiscalYear.LockedBy = actor;

                await context.SaveChangesAsync();
            }
            else
            {
                lockedAt = fiscalYear.LockedAt ?? DateTime.UtcNow;
            }

            var promoted = fiscalYears
                .Where(fy => fy.StartDate > fiscalYear.StartDate)
                .OrderBy(fy => fy.StartDate)
                .FirstOrDefault();

            _logger.LogInformation(
                "Fiscal year {FiscalYear} closed at {Timestamp} by {User}. Promoted fiscal year: {Promoted}.",
                fiscalYear.Name,
                lockedAt,
                actor,
                promoted?.Name ?? "None");

            return new FiscalYearCloseResult(fiscalYear, promoted);
        }
    }
}
