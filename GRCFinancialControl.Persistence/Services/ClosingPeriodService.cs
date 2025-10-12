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
        private readonly ApplicationDbContext _context;

        public ClosingPeriodService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<ClosingPeriod>> GetAllAsync()
        {
            return await _context.ClosingPeriods
                .OrderByDescending(cp => cp.PeriodEnd)
                .ThenByDescending(cp => cp.PeriodStart)
                .ToListAsync();
        }

        public async Task AddAsync(ClosingPeriod period)
        {
            await _context.ClosingPeriods.AddAsync(period);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(ClosingPeriod period)
        {
            _context.ClosingPeriods.Update(period);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var period = await _context.ClosingPeriods.FindAsync(id);
            if (period == null)
            {
                return;
            }

            var hasActuals = await _context.ActualsEntries.AnyAsync(a => a.ClosingPeriodId == id);
            if (hasActuals)
            {
                throw new InvalidOperationException("Cannot delete a closing period that is linked to imported margin data.");
            }

            _context.ClosingPeriods.Remove(period);
            await _context.SaveChangesAsync();
        }
    }
}
