using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GRCFinancialControl.Persistence.Services
{
    public class EngagementService : IEngagementService
    {
        private readonly ApplicationDbContext _context;

        public EngagementService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Engagement>> GetAllAsync()
        {
            return await _context.Engagements.Include(e => e.EngagementPapds).ThenInclude(ep => ep.Papd).ToListAsync();
        }

        public async Task<Engagement?> GetByIdAsync(int id)
        {
            return await _context.Engagements.Include(e => e.EngagementPapds).ThenInclude(ep => ep.Papd).FirstOrDefaultAsync(e => e.Id == id);
        }

        public async Task<Papd?> GetPapdForDateAsync(int engagementId, System.DateTime date)
        {
            var assignment = await _context.EngagementPapds
                .Include(ep => ep.Papd)
                .Where(ep => ep.EngagementId == engagementId && ep.EffectiveDate <= date)
                .OrderByDescending(ep => ep.EffectiveDate)
                .FirstOrDefaultAsync();

            return assignment?.Papd;
        }

        public async Task AddAsync(Engagement engagement)
        {
            await _context.Engagements.AddAsync(engagement);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Engagement engagement)
        {
            var existingEngagement = await GetByIdAsync(engagement.Id);
            if (existingEngagement != null)
            {
                _context.Entry(existingEngagement).CurrentValues.SetValues(engagement);

                // Remove old assignments
                _context.EngagementPapds.RemoveRange(existingEngagement.EngagementPapds);

                // Add new assignments
                foreach (var assignment in engagement.EngagementPapds)
                {
                    existingEngagement.EngagementPapds.Add(new EngagementPapd
                    {
                        PapdId = assignment.PapdId,
                        EffectiveDate = assignment.EffectiveDate
                    });
                }
            }

            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var engagement = await GetByIdAsync(id);
            if (engagement != null)
            {
                _context.Engagements.Remove(engagement);
                await _context.SaveChangesAsync();
            }
        }
    }
}