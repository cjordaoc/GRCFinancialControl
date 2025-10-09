using GRCFinancialControl.Data.SqliteConfigurations;
using Microsoft.EntityFrameworkCore;

namespace GRCFinancialControl.Data;

public class LocalSqliteContext : DbContext
{
    public LocalSqliteContext(DbContextOptions<LocalSqliteContext> options) : base(options)
    {
    }

    public DbSet<ParameterEntry> Parameters => Set<ParameterEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(LocalSqliteContext).Assembly,
            type => type.Namespace == typeof(ParameterEntryConfiguration).Namespace);
    }
}
