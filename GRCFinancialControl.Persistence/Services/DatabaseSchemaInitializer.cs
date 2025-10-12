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
    }
}
