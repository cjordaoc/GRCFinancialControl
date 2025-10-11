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
        private readonly ApplicationDbContext _context;

        public PlannedAllocationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<PlannedAllocation>> GetForEngagementAsync(int engagementId)
        {
            return await _context.PlannedAllocations
                .Where(pa => pa.EngagementId == engagementId)
                .ToListAsync();
        }

        public async Task SaveForEngagementAsync(int engagementId, List<PlannedAllocation> allocations)
        {
            // Remove existing allocations for this engagement
            var existingAllocations = await GetForEngagementAsync(engagementId);
            _context.PlannedAllocations.RemoveRange(existingAllocations);

            // Add the new allocations
            foreach (var allocation in allocations)
            {
                allocation.EngagementId = engagementId;
                await _context.PlannedAllocations.AddAsync(allocation);
            }

            await _context.SaveChangesAsync();
        }
    }
}