using GRCFinancialControl.Core.Enums;
using GRCFinancialControl.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace GRCFinancialControl.Persistence
{
    public class ApplicationDbContext : DbContext
    {
        public DbSet<Engagement> Engagements { get; set; }
        public DbSet<Papd> Papds { get; set; }
        public DbSet<Manager> Managers { get; set; }
        public DbSet<EngagementPapd> EngagementPapds { get; set; }
        public DbSet<EngagementManagerAssignment> EngagementManagerAssignments { get; set; }
        public DbSet<PlannedAllocation> PlannedAllocations { get; set; }
        public DbSet<ActualsEntry> ActualsEntries { get; set; }
        public DbSet<ExceptionEntry> Exceptions { get; set; }
        public DbSet<ClosingPeriod> ClosingPeriods { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<EngagementRankBudget> EngagementRankBudgets { get; set; }
        public DbSet<FinancialEvolution> FinancialEvolutions { get; set; }
        public DbSet<EngagementFiscalYearAllocation> EngagementFiscalYearAllocations { get; set; }
        public DbSet<EngagementFiscalYearRevenueAllocation> EngagementFiscalYearRevenueAllocations { get; set; }
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
                .Property(c => c.CustomerCode)
                .HasMaxLength(20);

            modelBuilder.Entity<Customer>()
                .Property(c => c.Name)
                .HasMaxLength(200);

            modelBuilder.Entity<Customer>()
                .HasIndex(c => c.CustomerCode)
                .IsUnique();

            modelBuilder.Entity<Engagement>()
                .HasAlternateKey(e => e.EngagementId);

            modelBuilder.Entity<ClosingPeriod>()
                .HasAlternateKey(cp => cp.Name);

            modelBuilder.Entity<ClosingPeriod>()
                .Property(cp => cp.Name)
                .HasMaxLength(100);

            modelBuilder.Entity<ClosingPeriod>()
                .Property(cp => cp.PeriodStart)
                .HasColumnType("datetime(6)");

            modelBuilder.Entity<ClosingPeriod>()
                .Property(cp => cp.PeriodEnd)
                .HasColumnType("datetime(6)");

            modelBuilder.Entity<Engagement>()
                .HasMany(e => e.EngagementPapds)
                .WithOne(ep => ep.Engagement)
                .HasForeignKey(ep => ep.EngagementId);

            modelBuilder.Entity<Engagement>()
                .HasMany(e => e.ManagerAssignments)
                .WithOne(ma => ma.Engagement)
                .HasForeignKey(ma => ma.EngagementId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Engagement>()
                .HasOne(e => e.Customer)
                .WithMany(c => c.Engagements)
                .HasForeignKey(e => e.CustomerId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Engagement>()
                .Property(e => e.Source)
                .HasConversion<string>()
                .HasMaxLength(20)
                .HasDefaultValue(EngagementSource.GrcProject);

            modelBuilder.Entity<Engagement>()
                .Property(e => e.Currency)
                .HasMaxLength(16);

            modelBuilder.Entity<Engagement>()
                .Property(e => e.Description)
                .HasMaxLength(255);

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
                .HasMaxLength(100);

            modelBuilder.Entity<Papd>()
                .HasMany<EngagementPapd>() // A PAPD can be linked to many EngagementPapd records
                .WithOne(ep => ep.Papd)
                .HasForeignKey(ep => ep.PapdId);

            modelBuilder.Entity<Papd>()
                .Property(p => p.Name)
                .HasMaxLength(200);

            modelBuilder.Entity<Papd>()
                .Property(p => p.Level)
                .HasConversion<string>()
                .HasMaxLength(100);

            modelBuilder.Entity<EngagementPapd>()
                .Property(ep => ep.EffectiveDate)
                .HasColumnType("datetime(6)");

            modelBuilder.Entity<Manager>()
                .Property(m => m.Name)
                .HasMaxLength(200);

            modelBuilder.Entity<Manager>()
                .Property(m => m.Email)
                .HasMaxLength(254);

            modelBuilder.Entity<Manager>()
                .Property(m => m.Position)
                .HasConversion<string>()
                .HasMaxLength(50);

            modelBuilder.Entity<Manager>()
                .HasMany(m => m.EngagementAssignments)
                .WithOne(a => a.Manager)
                .HasForeignKey(a => a.ManagerId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<EngagementManagerAssignment>()
                .HasKey(a => a.Id);

            modelBuilder.Entity<EngagementManagerAssignment>()
                .HasIndex(a => new { a.EngagementId, a.ManagerId, a.BeginDate });

            modelBuilder.Entity<EngagementManagerAssignment>()
                .Property(a => a.BeginDate)
                .HasColumnType("datetime(6)");

            modelBuilder.Entity<EngagementManagerAssignment>()
                .Property(a => a.EndDate)
                .HasColumnType("datetime(6)");

            modelBuilder.Entity<PlannedAllocation>()
                .HasOne(pa => pa.Engagement)
                .WithMany() // An engagement can have multiple planned allocations
                .HasForeignKey(pa => pa.EngagementId);

            modelBuilder.Entity<PlannedAllocation>()
                .HasOne(pa => pa.ClosingPeriod)
                .WithMany() // A closing period can have multiple planned allocations
                .HasForeignKey(pa => pa.ClosingPeriodId);

            modelBuilder.Entity<PlannedAllocation>()
                .Property(pa => pa.AllocatedHours)
                .HasPrecision(18, 2);

            modelBuilder.Entity<ActualsEntry>()
                .HasOne(ae => ae.Engagement)
                .WithMany() // An engagement can have multiple actual entries
                .HasForeignKey(ae => ae.EngagementId);

            modelBuilder.Entity<ActualsEntry>()
                .HasOne(ae => ae.ClosingPeriod)
                .WithMany(cp => cp.ActualsEntries)
                .HasForeignKey(ae => ae.ClosingPeriodId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ActualsEntry>()
                .Property(ae => ae.Date)
                .HasColumnType("datetime(6)");

            modelBuilder.Entity<ActualsEntry>()
                .Property(ae => ae.Hours)
                .HasPrecision(18, 2);

            modelBuilder.Entity<ActualsEntry>()
                .Property(ae => ae.ImportBatchId)
                .HasMaxLength(100);

            modelBuilder.Entity<ExceptionEntry>()
                .Property(ee => ee.SourceFile)
                .HasMaxLength(260);

            modelBuilder.Entity<ExceptionEntry>()
                .Property(ee => ee.Reason)
                .HasMaxLength(500);

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
                .Property(fe => fe.EngagementId)
                .HasMaxLength(64);

            modelBuilder.Entity<FinancialEvolution>()
                .Property(fe => fe.ClosingPeriodId)
                .HasMaxLength(100);

            modelBuilder.Entity<FinancialEvolution>()
                .Property(fe => fe.HoursData)
                .HasPrecision(18, 2);

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

            modelBuilder.Entity<EngagementFiscalYearAllocation>()
                .Property(e => e.PlannedHours)
                .HasPrecision(18, 2);

            modelBuilder.Entity<EngagementFiscalYearRevenueAllocation>()
                .HasKey(e => e.Id);

            modelBuilder.Entity<EngagementFiscalYearRevenueAllocation>()
                .HasOne(e => e.Engagement)
                .WithMany(e => e.RevenueAllocations)
                .HasForeignKey(e => e.EngagementId);

            modelBuilder.Entity<EngagementFiscalYearRevenueAllocation>()
                .HasOne(e => e.FiscalYear)
                .WithMany()
                .HasForeignKey(e => e.FiscalYearId);

            modelBuilder.Entity<EngagementFiscalYearRevenueAllocation>()
                .Property(e => e.PlannedValue)
                .HasPrecision(18, 2);

            modelBuilder.Entity<FiscalYear>()
                .HasKey(e => e.Id);

            modelBuilder.Entity<FiscalYear>()
                .Property(fy => fy.Name)
                .HasMaxLength(100);

            modelBuilder.Entity<FiscalYear>()
                .Property(fy => fy.StartDate)
                .HasColumnType("datetime(6)");

            modelBuilder.Entity<FiscalYear>()
                .Property(fy => fy.EndDate)
                .HasColumnType("datetime(6)");
        }
    }
}