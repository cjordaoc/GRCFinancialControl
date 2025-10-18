using GRCFinancialControl.Core.Enums;
using GRCFinancialControl.Core.Models;
using Invoices.Core.Enums;
using Invoices.Core.Models;
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
        public DbSet<InvoicePlan> InvoicePlans { get; set; }
        public DbSet<InvoicePlanEmail> InvoicePlanEmails { get; set; }
        public DbSet<InvoiceItem> InvoiceItems { get; set; }
        public DbSet<MailOutbox> MailOutboxEntries { get; set; }
        public DbSet<MailOutboxLog> MailOutboxLogs { get; set; }
        public DbSet<InvoiceNotificationPreview> InvoiceNotificationPreviews { get; set; }

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

            modelBuilder.Entity<ClosingPeriod>()
                .HasOne(cp => cp.FiscalYear)
                .WithMany(fy => fy.ClosingPeriods)
                .HasForeignKey(cp => cp.FiscalYearId)
                .OnDelete(DeleteBehavior.Restrict);

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
                .Property(e => e.OpeningValue)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Engagement>()
                .Property(e => e.LastEtcDate)
                .HasColumnType("datetime(6)");

            modelBuilder.Entity<Engagement>()
                .Property(e => e.ProposedNextEtcDate)
                .HasColumnType("datetime(6)");

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
                .Property(e => e.EstimatedToCompleteHours)
                .HasColumnName("EtcpHours")
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
                .HasOne(e => e.LastClosingPeriod)
                .WithMany(cp => cp.Engagements)
                .HasForeignKey(e => e.LastClosingPeriodId)
                .OnDelete(DeleteBehavior.SetNull);

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
                .HasIndex(pa => pa.ClosingPeriodId);

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

            modelBuilder.Entity<ActualsEntry>()
                .HasIndex(ae => new { ae.EngagementId, ae.Date });

            modelBuilder.Entity<ActualsEntry>()
                .HasIndex(ae => ae.ClosingPeriodId);

            modelBuilder.Entity<ActualsEntry>()
                .HasIndex(ae => ae.ImportBatchId);

            modelBuilder.Entity<ExceptionEntry>()
                .Property(ee => ee.Timestamp)
                .HasColumnType("datetime(6)")
                .ValueGeneratedOnAdd();

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
                .OnDelete(DeleteBehavior.Cascade);

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
                .Property(e => e.ToGoValue)
                .HasPrecision(18, 2);

            modelBuilder.Entity<EngagementFiscalYearRevenueAllocation>()
                .Property(e => e.ToDateValue)
                .HasPrecision(18, 2)
                .HasDefaultValue(0m);

            modelBuilder.Entity<EngagementFiscalYearRevenueAllocation>()
                .HasIndex(e => e.FiscalYearId);

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

            modelBuilder.Entity<InvoicePlan>()
                .ToTable("InvoicePlan");

            modelBuilder.Entity<InvoicePlan>()
                .Property(plan => plan.EngagementId)
                .HasMaxLength(64);

            modelBuilder.Entity<InvoicePlan>()
                .Property(plan => plan.Type)
                .HasConversion<string>()
                .HasMaxLength(16);

            modelBuilder.Entity<InvoicePlan>()
                .Property(plan => plan.CustomerFocalPointName)
                .HasMaxLength(120);

            modelBuilder.Entity<InvoicePlan>()
                .Property(plan => plan.CustomerFocalPointEmail)
                .HasMaxLength(200);

            modelBuilder.Entity<InvoicePlan>()
                .Property(plan => plan.CustomInstructions)
                .HasColumnType("text");

            modelBuilder.Entity<InvoicePlan>()
                .Property(plan => plan.FirstEmissionDate)
                .HasColumnType("date");

            modelBuilder.Entity<InvoicePlan>()
                .Property(plan => plan.CreatedAt)
                .HasColumnType("timestamp");

            modelBuilder.Entity<InvoicePlan>()
                .Property(plan => plan.UpdatedAt)
                .HasColumnType("timestamp");

            modelBuilder.Entity<InvoicePlan>()
                .HasIndex(plan => plan.EngagementId)
                .HasDatabaseName("IX_InvoicePlan_Engagement");

            modelBuilder.Entity<InvoicePlan>()
                .HasMany(plan => plan.Items)
                .WithOne(item => item.Plan)
                .HasForeignKey(item => item.PlanId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<InvoicePlan>()
                .HasMany(plan => plan.AdditionalEmails)
                .WithOne(email => email.Plan)
                .HasForeignKey(email => email.PlanId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<InvoicePlanEmail>()
                .ToTable("InvoicePlanEmail");

            modelBuilder.Entity<InvoicePlanEmail>()
                .Property(email => email.Email)
                .HasMaxLength(200);

            modelBuilder.Entity<InvoicePlanEmail>()
                .Property(email => email.CreatedAt)
                .HasColumnType("timestamp");

            modelBuilder.Entity<InvoicePlanEmail>()
                .HasIndex(email => email.PlanId)
                .HasDatabaseName("IX_InvoicePlanEmail_Plan");

            modelBuilder.Entity<InvoiceItem>()
                .ToTable("InvoiceItem");

            modelBuilder.Entity<InvoiceItem>()
                .Property(item => item.Percentage)
                .HasPrecision(9, 4);

            modelBuilder.Entity<InvoiceItem>()
                .Property(item => item.Amount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<InvoiceItem>()
                .Property(item => item.EmissionDate)
                .HasColumnType("date");

            modelBuilder.Entity<InvoiceItem>()
                .Property(item => item.DueDate)
                .HasColumnType("date");

            modelBuilder.Entity<InvoiceItem>()
                .Property(item => item.PayerCnpj)
                .HasMaxLength(20);

            modelBuilder.Entity<InvoiceItem>()
                .Property(item => item.PoNumber)
                .HasMaxLength(64);

            modelBuilder.Entity<InvoiceItem>()
                .Property(item => item.FrsNumber)
                .HasMaxLength(64);

            modelBuilder.Entity<InvoiceItem>()
                .Property(item => item.CustomerTicket)
                .HasMaxLength(64);

            modelBuilder.Entity<InvoiceItem>()
                .Property(item => item.DeliveryDescription)
                .HasMaxLength(255);

            modelBuilder.Entity<InvoiceItem>()
                .Property(item => item.Status)
                .HasConversion<string>()
                .HasMaxLength(16);

            modelBuilder.Entity<InvoiceItem>()
                .Property(item => item.RequestDate)
                .HasColumnType("date");

            modelBuilder.Entity<InvoiceItem>()
                .Property(item => item.EmittedAt)
                .HasColumnType("date");

            modelBuilder.Entity<InvoiceItem>()
                .Property(item => item.CanceledAt)
                .HasColumnType("date");

            modelBuilder.Entity<InvoiceItem>()
                .Property(item => item.CreatedAt)
                .HasColumnType("timestamp");

            modelBuilder.Entity<InvoiceItem>()
                .Property(item => item.UpdatedAt)
                .HasColumnType("timestamp");

            modelBuilder.Entity<InvoiceItem>()
                .HasIndex(item => new { item.PlanId, item.SeqNo })
                .IsUnique()
                .HasDatabaseName("UQ_InvoiceItem_PlanSeq");

            modelBuilder.Entity<InvoiceItem>()
                .HasIndex(item => item.EmissionDate)
                .HasDatabaseName("IX_InvoiceItem_EmissionDate");

            modelBuilder.Entity<InvoiceItem>()
                .HasIndex(item => item.Status)
                .HasDatabaseName("IX_InvoiceItem_Status");

            modelBuilder.Entity<InvoiceItem>()
                .HasOne(item => item.ReplacementItem)
                .WithMany()
                .HasForeignKey(item => item.ReplacementItemId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<MailOutbox>()
                .ToTable("MailOutbox");

            modelBuilder.Entity<MailOutbox>()
                .Property(outbox => outbox.NotificationDate)
                .HasColumnType("date");

            modelBuilder.Entity<MailOutbox>()
                .Property(outbox => outbox.ToName)
                .HasMaxLength(120);

            modelBuilder.Entity<MailOutbox>()
                .Property(outbox => outbox.ToEmail)
                .HasMaxLength(200);

            modelBuilder.Entity<MailOutbox>()
                .Property(outbox => outbox.Subject)
                .HasMaxLength(255);

            modelBuilder.Entity<MailOutbox>()
                .Property(outbox => outbox.SendToken)
                .HasMaxLength(36);

            modelBuilder.Entity<MailOutbox>()
                .Property(outbox => outbox.CreatedAt)
                .HasColumnType("timestamp");

            modelBuilder.Entity<MailOutbox>()
                .Property(outbox => outbox.SentAt)
                .HasColumnType("timestamp");

            modelBuilder.Entity<MailOutbox>()
                .HasIndex(outbox => outbox.NotificationDate)
                .HasDatabaseName("IX_MailOutbox_Notification");

            modelBuilder.Entity<MailOutbox>()
                .HasIndex(outbox => new { outbox.NotificationDate, outbox.SentAt, outbox.SendToken })
                .HasDatabaseName("IX_MailOutbox_Pending");

            modelBuilder.Entity<MailOutbox>()
                .HasOne(outbox => outbox.InvoiceItem)
                .WithMany(item => item.OutboxEntries)
                .HasForeignKey(outbox => outbox.InvoiceItemId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<MailOutboxLog>()
                .ToTable("MailOutboxLog");

            modelBuilder.Entity<MailOutboxLog>()
                .Property(log => log.AttemptAt)
                .HasColumnType("timestamp");

            modelBuilder.Entity<MailOutboxLog>()
                .Property(log => log.ErrorMessage)
                .HasMaxLength(500);

            modelBuilder.Entity<MailOutboxLog>()
                .HasIndex(log => log.OutboxId)
                .HasDatabaseName("IX_MailOutboxLog_Outbox");

            modelBuilder.Entity<MailOutboxLog>()
                .HasOne(log => log.Outbox)
                .WithMany(outbox => outbox.Logs)
                .HasForeignKey(log => log.OutboxId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<InvoiceNotificationPreview>()
                .HasNoKey()
                .ToView("vw_InvoiceNotifyOnDate");

            modelBuilder.Entity<InvoiceNotificationPreview>()
                .Property(view => view.NotifyDate)
                .HasColumnType("date");

            modelBuilder.Entity<InvoiceNotificationPreview>()
                .Property(view => view.EmissionDate)
                .HasColumnType("date");

            modelBuilder.Entity<InvoiceNotificationPreview>()
                .Property(view => view.ComputedDueDate)
                .HasColumnType("date");
            modelBuilder.Entity<FiscalYear>()
                .Property(fy => fy.AreaSalesTarget)
                .HasPrecision(18, 2)
                .HasDefaultValue(0m);

            modelBuilder.Entity<FiscalYear>()
                .Property(fy => fy.AreaRevenueTarget)
                .HasPrecision(18, 2)
                .HasDefaultValue(0m);

            modelBuilder.Entity<FiscalYear>()
                .Property(fy => fy.IsLocked)
                .HasDefaultValue(false);

            modelBuilder.Entity<FiscalYear>()
                .Property(fy => fy.LockedAt)
                .HasColumnType("datetime(6)");

            modelBuilder.Entity<FiscalYear>()
                .Property(fy => fy.LockedBy)
                .HasMaxLength(100);
        }
    }
}
