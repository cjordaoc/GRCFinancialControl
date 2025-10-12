using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GRCFinancialControl.Persistence
{
    public class DesignTimeApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            // This is for design-time tools. At runtime, the connection string will be provided by the settings service.
            optionsBuilder.UseSqlite("Data Source=design_time.db");

            return new ApplicationDbContext(optionsBuilder.Options);
        }
    }

    public class DesignTimeSettingsDbContextFactory : IDesignTimeDbContextFactory<SettingsDbContext>
    {
        public SettingsDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<SettingsDbContext>();
            optionsBuilder.UseSqlite("Data Source=settings.db");

            return new SettingsDbContext(optionsBuilder.Options);
        }
    }
}