using System.Collections.Generic;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Models;
using Microsoft.EntityFrameworkCore;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Persistence.Services
{
    public class CustomerService : ICustomerService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public CustomerService(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<List<Customer>> GetAllAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Customers.ToListAsync();
        }

        public async Task AddAsync(Customer customer)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            await context.Customers.AddAsync(customer);
            await context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Customer customer)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            context.Customers.Update(customer);
            await context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var customer = await context.Customers.FindAsync(id);
            if (customer != null)
            {
                context.Customers.Remove(customer);
                await context.SaveChangesAsync();
            }
        }

        public async Task DeleteDataAsync(int customerId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var customer = await context.Customers
                .Include(c => c.Engagements)
                .ThenInclude(e => e.EngagementPapds)
                .Include(c => c.Engagements)
                .ThenInclude(e => e.RankBudgets)
                .Include(c => c.Engagements)
                .ThenInclude(e => e.MarginEvolutions)
                .Include(c => c.Engagements)
                .ThenInclude(e => e.Allocations)
                .FirstOrDefaultAsync(c => c.Id == customerId);

            if (customer == null) return;

            foreach (var engagement in customer.Engagements)
            {
                var actualsToDelete = await context.ActualsEntries
                    .Where(a => a.EngagementId == engagement.Id)
                    .ToListAsync();
                context.ActualsEntries.RemoveRange(actualsToDelete);

                var plannedAllocationsToDelete = await context.PlannedAllocations
                    .Where(p => p.EngagementId == engagement.Id)
                    .ToListAsync();
                context.PlannedAllocations.RemoveRange(plannedAllocationsToDelete);

                context.EngagementPapds.RemoveRange(engagement.EngagementPapds);
                context.EngagementRankBudgets.RemoveRange(engagement.RankBudgets);
                context.MarginEvolutions.RemoveRange(engagement.MarginEvolutions);
                context.EngagementFiscalYearAllocations.RemoveRange(engagement.Allocations);
            }

            context.Engagements.RemoveRange(customer.Engagements);

            await context.SaveChangesAsync();
        }
    }
}