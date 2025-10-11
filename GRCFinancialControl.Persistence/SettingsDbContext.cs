using GRCFinancialControl.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace GRCFinancialControl.Persistence
{
    public class SettingsDbContext : DbContext
    {
        public DbSet<Setting> Settings { get; set; }

        public SettingsDbContext(DbContextOptions<SettingsDbContext> options)
            : base(options)
        {
        }
    }
}