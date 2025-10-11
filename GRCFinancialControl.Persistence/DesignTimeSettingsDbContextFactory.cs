using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GRCFinancialControl.Persistence
{
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