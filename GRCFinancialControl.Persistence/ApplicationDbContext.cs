using GRCFinancialControl.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace GRCFinancialControl.Persistence
{
    public class ApplicationDbContext : DbContext
    {
        public DbSet<Engagement> Engagements { get; set; }
        public DbSet<Papd> Papds { get; set; }
        public DbSet<EngagementPapd> EngagementPapds { get; set; }
        public DbSet<FiscalYear> FiscalYears { get; set; }
        public DbSet<PlannedAllocation> PlannedAllocations { get; set; }
        public DbSet<ActualsEntry> ActualsEntries { get; set; }
        public DbSet<ExceptionEntry> Exceptions { get; set; }
        public DbSet<ClosingPeriod> ClosingPeriods { get; set; }

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure composite key for EngagementPapd if not using an auto-incrementing Id
            // modelBuilder.Entity<EngagementPapd>()
            //     .HasKey(ep => new { ep.EngagementId, ep.PapdId, ep.EffectiveDate });

            // Configure relationships
            modelBuilder.Entity<Engagement>()
                .HasMany(e => e.EngagementPapds)
                .WithOne(ep => ep.Engagement)
                .HasForeignKey(ep => ep.EngagementId);

            modelBuilder.Entity<Papd>()
                .HasMany<EngagementPapd>() // A PAPD can be linked to many EngagementPapd records
                .WithOne(ep => ep.Papd)
                .HasForeignKey(ep => ep.PapdId);

            modelBuilder.Entity<PlannedAllocation>()
                .HasOne(pa => pa.Engagement)
                .WithMany() // An engagement can have multiple planned allocations
                .HasForeignKey(pa => pa.EngagementId);

            modelBuilder.Entity<ActualsEntry>()
                .HasOne(ae => ae.Engagement)
                .WithMany() // An engagement can have multiple actual entries
                .HasForeignKey(ae => ae.EngagementId);

            modelBuilder.Entity<ActualsEntry>()
                .HasOne(ae => ae.ClosingPeriod)
                .WithMany(cp => cp.ActualsEntries)
                .HasForeignKey(ae => ae.ClosingPeriodId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}