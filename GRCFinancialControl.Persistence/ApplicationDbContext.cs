using System;
using GRCFinancialControl.Core.Enums;
using GRCFinancialControl.Core.Models;
using Invoices.Core.Enums;
using Invoices.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

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
        public DbSet<Employee> Employees { get; set; }
        public DbSet<EngagementRankBudget> EngagementRankBudgets { get; set; }
        public DbSet<EngagementRankBudgetHistory> EngagementRankBudgetHistory { get; set; }
        public DbSet<RankMapping> RankMappings { get; set; }
        public DbSet<FinancialEvolution> FinancialEvolutions { get; set; }
        public DbSet<EngagementFiscalYearRevenueAllocation> EngagementFiscalYearRevenueAllocations { get; set; }
        public DbSet<FiscalYear> FiscalYears { get; set; }
        public DbSet<InvoicePlan> InvoicePlans { get; set; }
        public DbSet<InvoicePlanEmail> InvoicePlanEmails { get; set; }
        public DbSet<InvoiceItem> InvoiceItems { get; set; }
        public DbSet<InvoiceEmission> InvoiceEmissions { get; set; }
        public DbSet<MailOutbox> MailOutboxEntries { get; set; }
        public DbSet<MailOutboxLog> MailOutboxLogs { get; set; }
        public DbSet<InvoiceNotificationPreview> InvoiceNotificationPreviews { get; set; }
        public DbSet<EngagementAdditionalSale> EngagementAdditionalSales { get; set; }
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            var isMySql = Database.ProviderName?.Contains("MySql", StringComparison.OrdinalIgnoreCase) == true;

            modelBuilder.Entity<EngagementPapd>()
                .HasIndex(ep => new { ep.EngagementId, ep.PapdId })
                .IsUnique();

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

            var rankMappingBuilder = modelBuilder.Entity<RankMapping>();

            rankMappingBuilder.ToTable("RankMappings");

            rankMappingBuilder
                .Property(r => r.RawRank)
                .HasColumnName("RankCode")
                .HasMaxLength(50);

            rankMappingBuilder
                .Property(r => r.NormalizedRank)
                .HasColumnName("RankName")
                .HasMaxLength(100);

            rankMappingBuilder
                .Property(r => r.SpreadsheetRank)
                .HasColumnName("SpreadsheetRank")
                .HasMaxLength(100);

            rankMappingBuilder
                .Property(r => r.LastSeenAt)
                .HasMySqlColumnType("datetime(6)", isMySql);

            var rankCreatedAtProperty = rankMappingBuilder
                .Property(r => r.CreatedAt)
                .HasMySqlColumnType("datetime(6)", isMySql)
                .ValueGeneratedOnAdd();

            if (isMySql)
            {
                rankCreatedAtProperty.HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
            }
            else
            {
                rankCreatedAtProperty.HasDefaultValueSql("CURRENT_TIMESTAMP");
            }

            rankMappingBuilder
                .Property(r => r.IsActive)
                .HasDefaultValue(true);

            rankMappingBuilder
                .HasIndex(r => r.RawRank)
                .IsUnique();

            rankMappingBuilder
                .HasIndex(r => r.NormalizedRank);

            rankMappingBuilder
                .HasIndex(r => r.IsActive);

            rankMappingBuilder
                .HasIndex(r => r.SpreadsheetRank);

            modelBuilder.Entity<Engagement>()
                .HasAlternateKey(e => e.EngagementId);

            modelBuilder.Entity<ClosingPeriod>()
                .HasAlternateKey(cp => cp.Name);

            modelBuilder.Entity<ClosingPeriod>()
                .Property(cp => cp.Name)
                .HasMaxLength(100);

            modelBuilder.Entity<ClosingPeriod>()
                .Property(cp => cp.PeriodStart)
                .HasMySqlColumnType("datetime(6)", isMySql);

            modelBuilder.Entity<ClosingPeriod>()
                .Property(cp => cp.PeriodEnd)
                .HasMySqlColumnType("datetime(6)", isMySql);

            modelBuilder.Entity<ClosingPeriod>()
                .Property(cp => cp.IsLocked)
                .HasDefaultValue(false);

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
                .HasMySqlColumnType("datetime(6)", isMySql);

            modelBuilder.Entity<Engagement>()
                .Property(e => e.ProposedNextEtcDate)
                .HasMySqlColumnType("datetime(6)", isMySql);

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
                .Property(e => e.UnbilledRevenueDays);

            modelBuilder.Entity<Engagement>()
                .HasOne(e => e.LastClosingPeriod)
                .WithMany(cp => cp.Engagements)
                .HasForeignKey(e => e.LastClosingPeriodId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Papd>()
                .HasMany(p => p.EngagementPapds) // A PAPD can be linked to many EngagementPapd records
                .WithOne(ep => ep.Papd)
                .HasForeignKey(ep => ep.PapdId);

            modelBuilder.Entity<Papd>()
                .Property(p => p.Name)
                .HasMaxLength(200);

            modelBuilder.Entity<Papd>()
                .Property(p => p.Level)
                .HasConversion<string>()
                .HasMaxLength(100);

            modelBuilder.Entity<Papd>()
                .Property(p => p.WindowsLogin)
                .HasMaxLength(200);

            modelBuilder.Entity<Papd>()
                .HasIndex(p => p.WindowsLogin)
                .IsUnique();

            modelBuilder.Entity<Manager>()
                .Property(m => m.Name)
                .HasMaxLength(200);

            modelBuilder.Entity<Manager>()
                .Property(m => m.Email)
                .HasMaxLength(254);

            modelBuilder.Entity<Manager>()
                .Property(m => m.WindowsLogin)
                .HasMaxLength(200);

            modelBuilder.Entity<Manager>()
                .Property(m => m.Position)
                .HasConversion<string>()
                .HasMaxLength(50);

            modelBuilder.Entity<Manager>()
                .HasIndex(m => m.WindowsLogin)
                .IsUnique();

            modelBuilder.Entity<Manager>()
                .HasMany(m => m.EngagementAssignments)
                .WithOne(a => a.Manager)
                .HasForeignKey(a => a.ManagerId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Employee>()
                .HasKey(e => e.Gpn);

            modelBuilder.Entity<Employee>()
                .Property(e => e.Gpn)
                .HasMaxLength(20);

            modelBuilder.Entity<Employee>()
                .Property(e => e.EmployeeName)
                .HasMaxLength(200);

            modelBuilder.Entity<Employee>()
                .Property(e => e.Office)
                .HasMaxLength(100);

            modelBuilder.Entity<Employee>()
                .Property(e => e.CostCenter)
                .HasMaxLength(50);

            modelBuilder.Entity<Employee>()
                .Property(e => e.StartDate)
                .HasMySqlColumnType("date", isMySql);

            modelBuilder.Entity<Employee>()
                .Property(e => e.EndDate)
                .HasMySqlColumnType("date", isMySql);

            modelBuilder.Entity<Employee>()
                .Property(e => e.IsEyEmployee)
                .HasDefaultValue(true);

            modelBuilder.Entity<Employee>()
                .Property(e => e.IsContractor)
                .HasDefaultValue(false);

            modelBuilder.Entity<Employee>()
                .HasIndex(e => e.Office);

            modelBuilder.Entity<Employee>()
                .HasIndex(e => e.CostCenter);

            var managerAssignmentBuilder = modelBuilder.Entity<EngagementManagerAssignment>();
            managerAssignmentBuilder.HasKey(a => a.Id);

            var assignmentIdProperty = managerAssignmentBuilder
                .Property(a => a.Id)
                .ValueGeneratedOnAdd();

            if (isMySql)
            {
                assignmentIdProperty.UseMySqlIdentityColumn();
            }

            managerAssignmentBuilder
                .HasIndex(a => new { a.EngagementId, a.ManagerId })
                .IsUnique();

            modelBuilder.Entity<PlannedAllocation>()
                .ToTable("PlannedAllocations");

            modelBuilder.Entity<PlannedAllocation>()
                .HasOne(pa => pa.Engagement)
                .WithMany() // An engagement can have multiple planned allocations
                .HasForeignKey(pa => pa.EngagementId);

            modelBuilder.Entity<PlannedAllocation>()
                .HasOne(pa => pa.ClosingPeriod)
                .WithMany(cp => cp.PlannedAllocations) // A closing period can have multiple planned allocations
                .HasForeignKey(pa => pa.ClosingPeriodId);

            modelBuilder.Entity<PlannedAllocation>()
                .HasIndex(pa => pa.ClosingPeriodId);

            modelBuilder.Entity<PlannedAllocation>()
                .Property(pa => pa.AllocatedHours)
                .HasPrecision(18, 2)
                .HasDefaultValue(0m);

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
                .HasMySqlColumnType("datetime(6)", isMySql);

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
                .HasMySqlColumnType("datetime(6)", isMySql)
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
                .Property(rb => rb.BudgetHours)
                .HasPrecision(18, 2)
                .HasDefaultValue(0m);

            modelBuilder.Entity<EngagementRankBudget>()
                .Property(rb => rb.ConsumedHours)
                .HasPrecision(18, 2)
                .HasDefaultValue(0m);

            modelBuilder.Entity<EngagementRankBudget>()
                .Property(rb => rb.AdditionalHours)
                .HasPrecision(18, 2)
                .HasDefaultValue(0m);

            modelBuilder.Entity<EngagementRankBudget>()
                .Property(rb => rb.RemainingHours)
                .HasPrecision(18, 2)
                .HasDefaultValue(0m);

            modelBuilder.Entity<EngagementRankBudget>()
                .Property(rb => rb.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Green");

            modelBuilder.Entity<EngagementRankBudget>()
                .Property(rb => rb.CreatedAtUtc)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .ValueGeneratedOnAdd()
                .HasMySqlColumnType("timestamp", isMySql);

            modelBuilder.Entity<EngagementRankBudget>()
                .Property(rb => rb.UpdatedAtUtc)
                .HasMySqlColumnType("datetime(6)", isMySql);

            modelBuilder.Entity<EngagementRankBudget>()
                .HasIndex(rb => new { rb.EngagementId, rb.FiscalYearId, rb.RankName })
                .IsUnique();

            modelBuilder.Entity<EngagementRankBudget>()
                .HasOne(rb => rb.FiscalYear)
                .WithMany(fy => fy.RankBudgets)
                .HasForeignKey(rb => rb.FiscalYearId)
                .OnDelete(DeleteBehavior.Cascade);

            var historyBuilder = modelBuilder.Entity<EngagementRankBudgetHistory>();
            historyBuilder.ToTable("EngagementRankBudgetHistory");

            historyBuilder
                .Property(h => h.EngagementCode)
                .HasMaxLength(50);

            historyBuilder
                .Property(h => h.RankCode)
                .HasMaxLength(50);

            historyBuilder
                .Property(h => h.Hours)
                .HasPrecision(12, 2);

            historyBuilder
                .Property(h => h.UploadedAt)
                .HasMySqlColumnType("datetime", isMySql)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .ValueGeneratedOnAdd();

            historyBuilder
                .HasIndex(h => new { h.EngagementCode, h.RankCode, h.FiscalYearId, h.ClosingPeriodId })
                .HasDatabaseName("IX_History_Key")
                .IsUnique();

            modelBuilder.Entity<FinancialEvolution>()
                .ToTable("FinancialEvolution");

            modelBuilder.Entity<FinancialEvolution>()
                .HasOne(fe => fe.Engagement)
                .WithMany(e => e.FinancialEvolutions)
                .HasForeignKey(fe => fe.EngagementId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<FinancialEvolution>()
                .HasOne(fe => fe.FiscalYear)
                .WithMany()
                .HasForeignKey(fe => fe.FiscalYearId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<FinancialEvolution>()
                .Property(fe => fe.ClosingPeriodId)
                .HasMaxLength(100);

            // Hours Metrics
            modelBuilder.Entity<FinancialEvolution>()
                .Property(fe => fe.BudgetHours)
                .HasPrecision(18, 2);

            modelBuilder.Entity<FinancialEvolution>()
                .Property(fe => fe.ChargedHours)
                .HasPrecision(18, 2);

            modelBuilder.Entity<FinancialEvolution>()
                .Property(fe => fe.FYTDHours)
                .HasPrecision(18, 2);

            modelBuilder.Entity<FinancialEvolution>()
                .Property(fe => fe.AdditionalHours)
                .HasPrecision(18, 2);

            // Revenue Metrics
            modelBuilder.Entity<FinancialEvolution>()
                .Property(fe => fe.ValueData)
                .HasPrecision(18, 2);

            // Margin Metrics
            modelBuilder.Entity<FinancialEvolution>()
                .Property(fe => fe.BudgetMargin)
                .HasPrecision(18, 2);

            modelBuilder.Entity<FinancialEvolution>()
                .Property(fe => fe.ToDateMargin)
                .HasPrecision(18, 2);

            modelBuilder.Entity<FinancialEvolution>()
                .Property(fe => fe.FYTDMargin)
                .HasPrecision(18, 2);

            // Expense Metrics
            modelBuilder.Entity<FinancialEvolution>()
                .Property(fe => fe.ExpenseBudget)
                .HasPrecision(18, 2);

            modelBuilder.Entity<FinancialEvolution>()
                .Property(fe => fe.ExpensesToDate)
                .HasPrecision(18, 2);

            modelBuilder.Entity<FinancialEvolution>()
                .Property(fe => fe.FYTDExpenses)
                .HasPrecision(18, 2);

            modelBuilder.Entity<FinancialEvolution>()
                .Property(fe => fe.RevenueToGoValue)
                .HasPrecision(18, 2);

            modelBuilder.Entity<FinancialEvolution>()
                .Property(fe => fe.RevenueToDateValue)
                .HasPrecision(18, 2);

            modelBuilder.Entity<FinancialEvolution>()
                .HasIndex(fe => new { fe.EngagementId, fe.ClosingPeriodId })
                .IsUnique();

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
                .Property(e => e.LastUpdateDate)
                .HasMySqlColumnType("date", isMySql);

            var createdAtProperty = modelBuilder.Entity<EngagementFiscalYearRevenueAllocation>()
                .Property(e => e.CreatedAt)
                .HasMySqlColumnType("datetime(6)", isMySql)
                .ValueGeneratedOnAdd();

            var updatedAtProperty = modelBuilder.Entity<EngagementFiscalYearRevenueAllocation>()
                .Property(e => e.UpdatedAt)
                .HasMySqlColumnType("datetime(6)", isMySql)
                .ValueGeneratedOnAddOrUpdate();

            if (isMySql)
            {
                createdAtProperty.HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
                updatedAtProperty.HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
            }
            else
            {
                createdAtProperty.HasDefaultValueSql("CURRENT_TIMESTAMP");
                updatedAtProperty.HasDefaultValueSql("CURRENT_TIMESTAMP");
            }

            modelBuilder.Entity<EngagementFiscalYearRevenueAllocation>()
                .HasIndex(e => e.FiscalYearId);

            modelBuilder.Entity<EngagementAdditionalSale>()
                .HasOne(s => s.Engagement)
                .WithMany(e => e.AdditionalSales)
                .HasForeignKey(s => s.EngagementId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<EngagementAdditionalSale>()
                .Property(s => s.Description)
                .HasMaxLength(500)
                .IsRequired();

            modelBuilder.Entity<EngagementAdditionalSale>()
                .Property(s => s.OpportunityId)
                .HasMaxLength(100);

            modelBuilder.Entity<EngagementAdditionalSale>()
                .Property(s => s.Value)
                .HasPrecision(18, 2);

            modelBuilder.Entity<FiscalYear>()
                .HasKey(e => e.Id);

            modelBuilder.Entity<FiscalYear>()
                .Property(fy => fy.Name)
                .HasMaxLength(100);

            modelBuilder.Entity<FiscalYear>()
                .Property(fy => fy.StartDate)
                .HasMySqlColumnType("datetime(6)", isMySql);

            modelBuilder.Entity<FiscalYear>()
                .Property(fy => fy.EndDate)
                .HasMySqlColumnType("datetime(6)", isMySql);

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
                .HasMySqlColumnType("text", isMySql);

            modelBuilder.Entity<InvoicePlan>()
                .Property(plan => plan.AdditionalDetails)
                .HasMySqlColumnType("text", isMySql);

            modelBuilder.Entity<InvoicePlan>()
                .Property(plan => plan.FirstEmissionDate)
                .HasMySqlColumnType("date", isMySql);

            modelBuilder.Entity<InvoicePlan>()
                .Property(plan => plan.CreatedAt)
                .HasMySqlColumnType("timestamp", isMySql);

            modelBuilder.Entity<InvoicePlan>()
                .Property(plan => plan.UpdatedAt)
                .HasMySqlColumnType("timestamp", isMySql);

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
                .HasMySqlColumnType("timestamp", isMySql);

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
                .HasMySqlColumnType("date", isMySql);

            modelBuilder.Entity<InvoiceItem>()
                .Property(item => item.DueDate)
                .HasMySqlColumnType("date", isMySql);

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
                .Property(item => item.PaymentTypeCode)
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
                .HasMySqlColumnType("date", isMySql);

            modelBuilder.Entity<InvoiceItem>()
                .Property(item => item.CreatedAt)
                .HasMySqlColumnType("timestamp", isMySql);

            modelBuilder.Entity<InvoiceItem>()
                .Property(item => item.UpdatedAt)
                .HasMySqlColumnType("timestamp", isMySql);

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
                .HasMany(item => item.Emissions)
                .WithOne(emission => emission.InvoiceItem)
                .HasForeignKey(emission => emission.InvoiceItemId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<InvoiceEmission>()
                .ToTable("InvoiceEmission");

            modelBuilder.Entity<InvoiceEmission>()
                .Property(emission => emission.BzCode)
                .HasMaxLength(64);

            modelBuilder.Entity<InvoiceEmission>()
                .Property(emission => emission.CancelReason)
                .HasMaxLength(255);

            modelBuilder.Entity<InvoiceEmission>()
                .Property(emission => emission.EmittedAt)
                .HasMySqlColumnType("date", isMySql);

            modelBuilder.Entity<InvoiceEmission>()
                .Property(emission => emission.CanceledAt)
                .HasMySqlColumnType("date", isMySql);

            modelBuilder.Entity<InvoiceEmission>()
                .Property(emission => emission.CreatedAt)
                .HasMySqlColumnType("timestamp", isMySql);

            modelBuilder.Entity<InvoiceEmission>()
                .Property(emission => emission.UpdatedAt)
                .HasMySqlColumnType("timestamp", isMySql);

            modelBuilder.Entity<InvoiceEmission>()
                .HasIndex(emission => emission.InvoiceItemId)
                .HasDatabaseName("IX_InvoiceEmission_Item");

            modelBuilder.Entity<MailOutbox>()
                .ToTable("MailOutbox");

            modelBuilder.Entity<MailOutbox>()
                .Property(outbox => outbox.NotificationDate)
                .HasMySqlColumnType("date", isMySql);

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
                .HasMySqlColumnType("timestamp", isMySql);

            modelBuilder.Entity<MailOutbox>()
                .Property(outbox => outbox.SentAt)
                .HasMySqlColumnType("timestamp", isMySql);

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
                .HasMySqlColumnType("timestamp", isMySql);

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
                .HasMySqlColumnType("date", isMySql);

            modelBuilder.Entity<InvoiceNotificationPreview>()
                .Property(view => view.EmissionDate)
                .HasMySqlColumnType("date", isMySql);

            modelBuilder.Entity<InvoiceNotificationPreview>()
                .Property(view => view.ComputedDueDate)
                .HasMySqlColumnType("date", isMySql);
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
                .HasMySqlColumnType("datetime(6)", isMySql);

            modelBuilder.Entity<FiscalYear>()
                .Property(fy => fy.LockedBy)
                .HasMaxLength(100);
        }
    }
}
