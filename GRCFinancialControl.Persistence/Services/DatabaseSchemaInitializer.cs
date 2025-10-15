using System;
using System.Threading.Tasks;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GRCFinancialControl.Persistence.Services
{
    public class DatabaseSchemaInitializer : IDatabaseSchemaInitializer
    {
        private const string DisableForeignKeyChecksSql = "SET FOREIGN_KEY_CHECKS = 0;";
        private const string EnableForeignKeyChecksSql = "SET FOREIGN_KEY_CHECKS = 1;";

        private static readonly string[] DeleteStatements =
        {
            "DELETE FROM `ActualsEntries`;",
            "DELETE FROM `PlannedAllocations`;",
            "DELETE FROM `EngagementFiscalYearAllocations`;",
            "DELETE FROM `EngagementFiscalYearRevenueAllocations`;",
            "DELETE FROM `EngagementRankBudgets`;",
            "DELETE FROM `FinancialEvolution`;",
            "DELETE FROM `EngagementManagerAssignments`;",
            "DELETE FROM `EngagementPapds`;",
            "DELETE FROM `Exceptions`;",
            "DELETE FROM `Engagements`;",
            "DELETE FROM `Customers`;",
            "DELETE FROM `Managers`;",
            "DELETE FROM `Papds`;",
            "DELETE FROM `ClosingPeriods`;",
            "DELETE FROM `FiscalYears`;"
        };

        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public DatabaseSchemaInitializer(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            ArgumentNullException.ThrowIfNull(contextFactory);

            _contextFactory = contextFactory;
        }

        public async Task EnsureSchemaAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            ArgumentNullException.ThrowIfNull(context);
            await context.Database.EnsureCreatedAsync().ConfigureAwait(false);
        }

        public async Task ClearAllDataAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            ArgumentNullException.ThrowIfNull(context);
            await using var transaction = await context.Database.BeginTransactionAsync().ConfigureAwait(false);

            await context.Database.ExecuteSqlRawAsync(DisableForeignKeyChecksSql).ConfigureAwait(false);

            try
            {
                foreach (var statement in DeleteStatements)
                {
                    await context.Database.ExecuteSqlRawAsync(statement).ConfigureAwait(false);
                }

                await context.Database.ExecuteSqlRawAsync(EnableForeignKeyChecksSql).ConfigureAwait(false);
                await transaction.CommitAsync().ConfigureAwait(false);
            }
            catch
            {
                await transaction.RollbackAsync().ConfigureAwait(false);
                await context.Database.ExecuteSqlRawAsync(EnableForeignKeyChecksSql).ConfigureAwait(false);
                throw;
            }
        }
    }
}
