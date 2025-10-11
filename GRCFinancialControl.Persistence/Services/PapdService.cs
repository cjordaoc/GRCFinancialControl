using System.Collections.Generic;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GRCFinancialControl.Persistence.Services
{
    public class PapdService : IPapdService
    {
        private readonly ApplicationDbContext _context;

        public PapdService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Papd>> GetAllAsync()
        {
            return await _context.Papds.ToListAsync();
        }

        public async Task AddAsync(Papd papd)
        {
            await _context.Papds.AddAsync(papd);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Papd papd)
        {
            _context.Papds.Update(papd);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var papd = await _context.Papds.FindAsync(id);
            if (papd != null)
            {
                _context.Papds.Remove(papd);
                await _context.SaveChangesAsync();
            }
        }
    }
}