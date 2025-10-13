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
            await using var context = await _contextFactory.CreateDbContextAsync();
            var tableNames = new[]
            {
                "ActualsEntries", "PlannedAllocations", "EngagementFiscalYearAllocations",
                "EngagementRankBudgets", "MarginEvolutions", "EngagementPapds",
                "Engagements", "Customers", "Papds", "ClosingPeriods", "FiscalYears"
            };

            foreach (var tableName in tableNames)
            {
                await context.Database.ExecuteSqlRawAsync($"DELETE FROM `{tableName}`");
            }
        }
    }
}
