using System.Threading.Tasks;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GRCFinancialControl.Persistence.Services
{
    public class DatabaseSchemaInitializer : IDatabaseSchemaInitializer
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public DatabaseSchemaInitializer(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task EnsureSchemaAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            await context.Database.EnsureCreatedAsync().ConfigureAwait(false);
        }

        public async Task ClearAllDataAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            await using var transaction = await context.Database.BeginTransactionAsync().ConfigureAwait(false);

            var deleteStatements = new[]
            {
                "DELETE FROM `ActualsEntries`;",
                "DELETE FROM `PlannedAllocations`;",
                "DELETE FROM `EngagementFiscalYearAllocations`;",
                "DELETE FROM `EngagementRankBudgets`;",
                "DELETE FROM `FinancialEvolution`;",
                "DELETE FROM `EngagementPapds`;",
                "DELETE FROM `Exceptions`;",
                "DELETE FROM `Engagements`;",
                "DELETE FROM `Customers`;",
                "DELETE FROM `Papds`;",
                "DELETE FROM `ClosingPeriods`;",
                "DELETE FROM `FiscalYears`;"
            };

            await context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 0;").ConfigureAwait(false);

            try
            {
                foreach (var statement in deleteStatements)
                {
                    await context.Database.ExecuteSqlRawAsync(statement).ConfigureAwait(false);
                }

                await context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 1;").ConfigureAwait(false);
                await transaction.CommitAsync().ConfigureAwait(false);
            }
            catch
            {
                await transaction.RollbackAsync().ConfigureAwait(false);
                await context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 1;").ConfigureAwait(false);
                throw;
            }
        }
    }
}
