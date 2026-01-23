using GRCFinancialControl.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace GRCFinancialControl.Persistence
{
    public class SettingsDbContext(DbContextOptions<SettingsDbContext> options) : DbContext(options)
    {
        public DbSet<Setting> Settings { get; set; }
    }
}