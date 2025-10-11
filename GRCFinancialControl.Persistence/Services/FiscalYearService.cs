using System.Collections.Generic;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GRCFinancialControl.Persistence.Services
{
    public class FiscalYearService : IFiscalYearService
    {
        private readonly ApplicationDbContext _context;

        public FiscalYearService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<FiscalYear>> GetAllAsync()
        {
            return await _context.FiscalYears.ToListAsync();
        }

        public async Task AddAsync(FiscalYear fiscalYear)
        {
            await _context.FiscalYears.AddAsync(fiscalYear);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(FiscalYear fiscalYear)
        {
            _context.FiscalYears.Update(fiscalYear);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var fiscalYear = await _context.FiscalYears.FindAsync(id);
            if (fiscalYear != null)
            {
                _context.FiscalYears.Remove(fiscalYear);
                await _context.SaveChangesAsync();
            }
        }
    }
}