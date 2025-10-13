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

            var truncateStatements = new[]
            {
                "TRUNCATE TABLE `ActualsEntries`;",
                "TRUNCATE TABLE `PlannedAllocations`;",
                "TRUNCATE TABLE `EngagementFiscalYearAllocations`;",
                "TRUNCATE TABLE `EngagementRankBudgets`;",
                "TRUNCATE TABLE `MarginEvolutions`;",
                "TRUNCATE TABLE `EngagementPapds`;",
                "TRUNCATE TABLE `Exceptions`;",
                "TRUNCATE TABLE `Engagements`;",
                "TRUNCATE TABLE `Customers`;",
                "TRUNCATE TABLE `Papds`;",
                "TRUNCATE TABLE `ClosingPeriods`;",
                "TRUNCATE TABLE `FiscalYears`;"
            };

            await context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 0;").ConfigureAwait(false);

            foreach (var statement in truncateStatements)
            {
                await context.Database.ExecuteSqlRawAsync(statement).ConfigureAwait(false);
            }

            await context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 1;").ConfigureAwait(false);
        }
    }
}
