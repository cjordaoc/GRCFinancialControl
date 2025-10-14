using GRCFinancialControl.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace GRCFinancialControl.Persistence
{
    public class ApplicationDbContext : DbContext
    {
        public DbSet<Engagement> Engagements { get; set; }
        public DbSet<Papd> Papds { get; set; }
        public DbSet<EngagementPapd> EngagementPapds { get; set; }
        public DbSet<PlannedAllocation> PlannedAllocations { get; set; }
        public DbSet<ActualsEntry> ActualsEntries { get; set; }
        public DbSet<ExceptionEntry> Exceptions { get; set; }
        public DbSet<ClosingPeriod> ClosingPeriods { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<EngagementRankBudget> EngagementRankBudgets { get; set; }
        public DbSet<FinancialEvolution> FinancialEvolutions { get; set; }
        public DbSet<EngagementFiscalYearAllocation> EngagementFiscalYearAllocations { get; set; }
        public DbSet<FiscalYear> FiscalYears { get; set; }

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
            modelBuilder.Entity<Customer>()
                .Property(c => c.CustomerID)
                .HasMaxLength(20);

            modelBuilder.Entity<Customer>()
                .HasIndex(c => c.CustomerID)
                .IsUnique();

            modelBuilder.Entity<Engagement>()
                .HasAlternateKey(e => e.EngagementId);

            modelBuilder.Entity<ClosingPeriod>()
                .HasAlternateKey(cp => cp.Name);

            modelBuilder.Entity<Engagement>()
                .HasMany(e => e.EngagementPapds)
                .WithOne(ep => ep.Engagement)
                .HasForeignKey(ep => ep.EngagementId);

            modelBuilder.Entity<Engagement>()
                .HasOne(e => e.Customer)
                .WithMany(c => c.Engagements)
                .HasForeignKey(e => e.CustomerId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Engagement>()
                .Property(e => e.Currency)
                .HasMaxLength(16);

            modelBuilder.Entity<Engagement>()
                .Property(e => e.MarginPctBudget)
                .HasPrecision(9, 4);

            modelBuilder.Entity<Engagement>()
                .Property(e => e.MarginPctEtcp)
                .HasPrecision(9, 4);

            modelBuilder.Entity<Engagement>()
                .Property(e => e.StatusText)
                .HasMaxLength(100);

            modelBuilder.Entity<Engagement>()
                .HasMany(e => e.RankBudgets)
                .WithOne(rb => rb.Engagement)
                .HasForeignKey(rb => rb.EngagementId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Engagement>()
                .Property(e => e.OpeningExpenses)
                .HasPrecision(18, 2)
                .HasDefaultValue(0m);

            modelBuilder.Entity<Engagement>()
                .Property(e => e.InitialHoursBudget)
                .HasPrecision(18, 2)
                .HasDefaultValue(0m);

            modelBuilder.Entity<Engagement>()
                .Property(e => e.EtcpHours)
                .HasPrecision(18, 2)
                .HasDefaultValue(0m);

            modelBuilder.Entity<Engagement>()
                .Property(e => e.ValueEtcp)
                .HasPrecision(18, 2)
                .HasDefaultValue(0m);

            modelBuilder.Entity<Engagement>()
                .Property(e => e.ExpensesEtcp)
                .HasPrecision(18, 2)
                .HasDefaultValue(0m);

            modelBuilder.Entity<Engagement>()
                .Property(e => e.LastClosingPeriodId)
                .HasMaxLength(16);

            modelBuilder.Entity<Papd>()
                .HasMany<EngagementPapd>() // A PAPD can be linked to many EngagementPapd records
                .WithOne(ep => ep.Papd)
                .HasForeignKey(ep => ep.PapdId);

            modelBuilder.Entity<Papd>()
                .Property(p => p.Level)
                .HasConversion<string>()
                .HasMaxLength(100);

            modelBuilder.Entity<PlannedAllocation>()
                .HasOne(pa => pa.Engagement)
                .WithMany() // An engagement can have multiple planned allocations
                .HasForeignKey(pa => pa.EngagementId);

            modelBuilder.Entity<PlannedAllocation>()
                .HasOne(pa => pa.ClosingPeriod)
                .WithMany() // A closing period can have multiple planned allocations
                .HasForeignKey(pa => pa.ClosingPeriodId);

            modelBuilder.Entity<ActualsEntry>()
                .HasOne(ae => ae.Engagement)
                .WithMany() // An engagement can have multiple actual entries
                .HasForeignKey(ae => ae.EngagementId);

            modelBuilder.Entity<ActualsEntry>()
                .HasOne(ae => ae.ClosingPeriod)
                .WithMany(cp => cp.ActualsEntries)
                .HasForeignKey(ae => ae.ClosingPeriodId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<EngagementRankBudget>()
                .Property(rb => rb.RankName)
                .HasMaxLength(100);

            modelBuilder.Entity<EngagementRankBudget>()
                .Property(rb => rb.Hours)
                .HasPrecision(18, 2)
                .HasDefaultValue(0m);

            modelBuilder.Entity<EngagementRankBudget>()
                .Property(rb => rb.CreatedAtUtc)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            modelBuilder.Entity<EngagementRankBudget>()
                .HasIndex(rb => new { rb.EngagementId, rb.RankName })
                .IsUnique();

            modelBuilder.Entity<FinancialEvolution>()
                .ToTable("FinancialEvolution");

            modelBuilder.Entity<FinancialEvolution>()
                .HasOne(fe => fe.Engagement)
                .WithMany(e => e.FinancialEvolutions)
                .HasForeignKey(fe => fe.EngagementId)
                .HasPrincipalKey(e => e.EngagementId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<FinancialEvolution>()
                .Property(fe => fe.HoursData)
                .HasPrecision(10, 1);

            modelBuilder.Entity<FinancialEvolution>()
                .Property(fe => fe.ValueData)
                .HasPrecision(18, 2);

            modelBuilder.Entity<FinancialEvolution>()
                .Property(fe => fe.MarginData)
                .HasPrecision(9, 4);

            modelBuilder.Entity<FinancialEvolution>()
                .Property(fe => fe.ExpenseData)
                .HasPrecision(18, 2);

            modelBuilder.Entity<FinancialEvolution>()
                .HasIndex(fe => new { fe.EngagementId, fe.ClosingPeriodId })
                .IsUnique();

            modelBuilder.Entity<EngagementFiscalYearAllocation>()
                .HasKey(e => e.Id);

            modelBuilder.Entity<EngagementFiscalYearAllocation>()
                .HasOne(e => e.Engagement)
                .WithMany(e => e.Allocations)
                .HasForeignKey(e => e.EngagementId);

            modelBuilder.Entity<EngagementFiscalYearAllocation>()
                .HasOne(e => e.FiscalYear)
                .WithMany()
                .HasForeignKey(e => e.FiscalYearId);

            modelBuilder.Entity<FiscalYear>()
                .HasKey(e => e.Id);
        }
    }
}