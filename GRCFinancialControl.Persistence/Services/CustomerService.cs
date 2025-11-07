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
    /// <summary>
    /// Manages customer entities including CRUD operations and customer-specific data deletion.
    /// Extends ContextFactoryCrudService for standard database operations.
    /// </summary>
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

        /// <summary>
        /// Deletes all engagement and financial data associated with a customer.
        /// Cascades through: Actuals, Planned Allocations, PAPD assignments, Rank Budgets,
        /// Financial Evolution snapshots, Revenue Allocations, and Engagements.
        /// </summary>
        public async Task DeleteDataAsync(int customerId)
        {
            if (customerId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(customerId), customerId, "Customer identifier must be positive.");
            }

            await using var context = await CreateContextAsync().ConfigureAwait(false);

            var engagementKeyPairs = await context.Engagements
                .Where(e => e.CustomerId == customerId)
                .Select(e => new { e.Id, e.EngagementId })
                .ToListAsync()
                .ConfigureAwait(false);

            if (engagementKeyPairs.Count == 0)
            {
                return;
            }

            var engagementIds = engagementKeyPairs
                .Select(pair => pair.Id)
                .ToList();

            await context.ActualsEntries
                .Where(a => engagementIds.Contains(a.EngagementId))
                .ExecuteDeleteAsync()
                .ConfigureAwait(false);

            await context.PlannedAllocations
                .Where(p => engagementIds.Contains(p.EngagementId))
                .ExecuteDeleteAsync()
                .ConfigureAwait(false);

            await context.EngagementPapds
                .Where(ep => engagementIds.Contains(ep.EngagementId))
                .ExecuteDeleteAsync()
                .ConfigureAwait(false);

            await context.EngagementRankBudgets
                .Where(rb => engagementIds.Contains(rb.EngagementId))
                .ExecuteDeleteAsync()
                .ConfigureAwait(false);

            await context.FinancialEvolutions
                .Where(fe => engagementIds.Contains(fe.EngagementId))
                .ExecuteDeleteAsync()
                .ConfigureAwait(false);

            await context.EngagementFiscalYearRevenueAllocations
                .Where(a => engagementIds.Contains(a.EngagementId))
                .ExecuteDeleteAsync()
                .ConfigureAwait(false);

            await context.Engagements
                .Where(e => engagementIds.Contains(e.Id))
                .ExecuteDeleteAsync()
                .ConfigureAwait(false);
        }
    }
}