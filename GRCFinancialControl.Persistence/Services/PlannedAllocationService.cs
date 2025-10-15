using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GRCFinancialControl.Persistence.Services
{
    public class PlannedAllocationService : IPlannedAllocationService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public PlannedAllocationService(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<List<PlannedAllocation>> GetAllocationsForEngagementAsync(int engagementId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.PlannedAllocations
                .AsNoTracking()
                .Where(pa => pa.EngagementId == engagementId)
                .Include(pa => pa.ClosingPeriod)
                .ToListAsync();
        }

        public async Task SaveAllocationsForEngagementAsync(int engagementId, List<PlannedAllocation> allocations)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            await context.PlannedAllocations
                .Where(pa => pa.EngagementId == engagementId)
                .ExecuteDeleteAsync();

            // Add the new allocations
            foreach (var allocation in allocations)
            {
                allocation.EngagementId = engagementId;
                await context.PlannedAllocations.AddAsync(allocation);
            }

            await context.SaveChangesAsync();
        }
    }
}
