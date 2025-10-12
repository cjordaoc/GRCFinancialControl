using System;
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
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public EngagementService(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<List<Engagement>> GetAllAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Engagements.Include(e => e.EngagementPapds).ThenInclude(ep => ep.Papd).ToListAsync();
        }

        public async Task<Engagement?> GetByIdAsync(int id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Engagements.Include(e => e.EngagementPapds).ThenInclude(ep => ep.Papd).FirstOrDefaultAsync(e => e.Id == id);
        }

        public async Task<Papd?> GetPapdForDateAsync(int engagementId, System.DateTime date)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var assignment = await context.EngagementPapds
                .Include(ep => ep.Papd)
                .Where(ep => ep.EngagementId == engagementId && ep.EffectiveDate <= date)
                .OrderByDescending(ep => ep.EffectiveDate)
                .FirstOrDefaultAsync();

            return assignment?.Papd;
        }

        public async Task AddAsync(Engagement engagement)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            await context.Engagements.AddAsync(engagement);
            await context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Engagement engagement)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var existingEngagement = await context.Engagements
                .Include(e => e.EngagementPapds)
                .FirstOrDefaultAsync(e => e.Id == engagement.Id);

            if (existingEngagement != null)
            {
                context.Entry(existingEngagement).CurrentValues.SetValues(engagement);

                context.EngagementPapds.RemoveRange(existingEngagement.EngagementPapds);

                foreach (var assignment in engagement.EngagementPapds)
                {
                    var papdId = assignment.PapdId;
                    if (papdId == 0 && assignment.Papd != null)
                    {
                        papdId = assignment.Papd.Id;
                    }

                    if (papdId == 0)
                    {
                        throw new InvalidOperationException("Cannot update engagement PAPD assignments without a valid PapdId.");
                    }

                    existingEngagement.EngagementPapds.Add(new EngagementPapd
                    {
                        PapdId = papdId,
                        EffectiveDate = assignment.EffectiveDate
                    });
                }

                await context.SaveChangesAsync();
            }
        }

        public async Task DeleteAsync(int id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var engagement = await context.Engagements
                .Include(e => e.EngagementPapds)
                .FirstOrDefaultAsync(e => e.Id == id);
            if (engagement != null)
            {
                context.Engagements.Remove(engagement);
                await context.SaveChangesAsync();
            }
        }
    }
}