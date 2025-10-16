using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GRCFinancialControl.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GRCFinancialControl.Persistence.Services.Infrastructure
{
    public static class FiscalYearLockGuard
    {
        public static async Task EnsureFiscalYearUnlockedAsync(ApplicationDbContext context, int fiscalYearId, string operation)
        {
            if (fiscalYearId <= 0)
            {
                return;
            }

            await EnsureFiscalYearsUnlockedAsync(context, new[] { fiscalYearId }, operation);
        }

        public static async Task EnsureFiscalYearsUnlockedAsync(ApplicationDbContext context, IEnumerable<int> fiscalYearIds, string operation)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(fiscalYearIds);
            ArgumentException.ThrowIfNullOrEmpty(operation);

            var ids = fiscalYearIds
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (ids.Count == 0)
            {
                return;
            }

            var lockedFiscalYears = await context.FiscalYears
                .Where(fy => ids.Contains(fy.Id) && fy.IsLocked)
                .Select(fy => new { fy.Id, fy.Name })
                .ToListAsync();

            if (lockedFiscalYears.Count == 0)
            {
                return;
            }

            var formatted = string.Join(", ", lockedFiscalYears.Select(fy => string.IsNullOrWhiteSpace(fy.Name)
                ? $"Id={fy.Id}"
                : $"{fy.Name} (Id={fy.Id})"));

            throw new InvalidOperationException($"Cannot {operation} because fiscal year(s) {formatted} are locked.");
        }

        public static async Task EnsureClosingPeriodUnlockedAsync(ApplicationDbContext context, int closingPeriodId, string operation)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentException.ThrowIfNullOrEmpty(operation);

            if (closingPeriodId <= 0)
            {
                return;
            }

            var closingPeriod = await context.ClosingPeriods
                .Include(cp => cp.FiscalYear)
                .FirstOrDefaultAsync(cp => cp.Id == closingPeriodId);

            if (closingPeriod?.FiscalYear == null || !closingPeriod.FiscalYear.IsLocked)
            {
                return;
            }

            var fiscalYearName = string.IsNullOrWhiteSpace(closingPeriod.FiscalYear.Name)
                ? $"Id={closingPeriod.FiscalYear.Id}"
                : closingPeriod.FiscalYear.Name;

            throw new InvalidOperationException($"Cannot {operation} because fiscal year '{fiscalYearName}' is locked.");
        }
    }
}
