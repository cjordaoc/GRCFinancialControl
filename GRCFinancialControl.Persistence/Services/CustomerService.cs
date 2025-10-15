using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Infrastructure;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GRCFinancialControl.Persistence.Services
{
    public class CustomerService : ContextFactoryCrudService<Customer>, ICustomerService
    {
        public CustomerService(IDbContextFactory<ApplicationDbContext> contextFactory)
            : base(contextFactory)
        {
        }

        protected override DbSet<Customer> Set(ApplicationDbContext context) => context.Customers;

        public Task<List<Customer>> GetAllAsync() => GetAllInternalAsync(static query => query.AsNoTracking().OrderBy(c => c.Name));

        public Task AddAsync(Customer customer) => AddEntityAsync(customer);

        public Task UpdateAsync(Customer customer) => UpdateEntityAsync(customer);

        public Task DeleteAsync(int id) => DeleteEntityAsync(id);

        public async Task DeleteDataAsync(int customerId)
        {
            if (customerId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(customerId), customerId, "Customer identifier must be positive.");
            }

            await using var context = await CreateContextAsync();

            var engagementKeyPairs = await context.Engagements
                .Where(e => e.CustomerId == customerId)
                .Select(e => new { e.Id, e.EngagementId })
                .ToListAsync();

            if (engagementKeyPairs.Count == 0)
            {
                return;
            }

            var engagementIds = engagementKeyPairs
                .Select(pair => pair.Id)
                .ToList();

            var engagementBusinessKeys = engagementKeyPairs
                .Select(pair => pair.EngagementId)
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .ToList();

            await context.ActualsEntries
                .Where(a => engagementIds.Contains(a.EngagementId))
                .ExecuteDeleteAsync();

            await context.PlannedAllocations
                .Where(p => engagementIds.Contains(p.EngagementId))
                .ExecuteDeleteAsync();

            await context.EngagementPapds
                .Where(ep => engagementIds.Contains(ep.EngagementId))
                .ExecuteDeleteAsync();

            await context.EngagementRankBudgets
                .Where(rb => engagementIds.Contains(rb.EngagementId))
                .ExecuteDeleteAsync();

            if (engagementBusinessKeys.Count > 0)
            {
                await context.FinancialEvolutions
                    .Where(fe => engagementBusinessKeys.Contains(fe.EngagementId))
                    .ExecuteDeleteAsync();
            }

            await context.EngagementFiscalYearAllocations
                .Where(a => engagementIds.Contains(a.EngagementId))
                .ExecuteDeleteAsync();

            await context.EngagementFiscalYearRevenueAllocations
                .Where(a => engagementIds.Contains(a.EngagementId))
                .ExecuteDeleteAsync();

            await context.Engagements
                .Where(e => engagementIds.Contains(e.Id))
                .ExecuteDeleteAsync();
        }
    }
}