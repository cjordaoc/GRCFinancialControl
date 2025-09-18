// ======================================================================
// Area Financial Control - EF Core 8 (Pomelo) Models + DbContext
// Synchronous only (no async/await).
// Target: .NET 8 WinForms
// ======================================================================
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GRCFinancialControl.Data
{
    [Index(nameof(SystemCode), IsUnique = true)]
    public class DimSourceSystem
    {
        [Key] public ushort SourceSystemId { get; set; }
        [Required, MaxLength(50)] public string SystemCode { get; set; } = null!;
        [Required, MaxLength(100)] public string SystemName { get; set; } = null!;
    }

    public partial class DimMeasurementPeriod
    {
        [Key] public ushort MeasurementPeriodId { get; set; }
        [Required, MaxLength(100)] public string Description { get; set; } = null!;
        [Required] public DateOnly StartDate { get; set; }
        [Required] public DateOnly EndDate { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime UpdatedUtc { get; set; }
    }

    public partial class DimFiscalYear
    {
        [Key] public ushort FiscalYearId { get; set; }
        [Required, MaxLength(100)] public string Description { get; set; } = null!;
        [Required] public DateOnly DateFrom { get; set; }
        [Required] public DateOnly DateTo { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime UpdatedUtc { get; set; }
    }

    public class DimEngagement
    {
        [Key, MaxLength(64)] public string EngagementId { get; set; } = null!;
        [MaxLength(255)] public string? EngagementTitle { get; set; }
        public bool IsActive { get; set; } = true;
        [Column(TypeName = "text")] public string? EngagementPartner { get; set; }
        [Column(TypeName = "text")] public string? EngagementManager { get; set; }
        public double OpeningMargin { get; set; }
        public double CurrentMargin { get; set; }
        public DateTime? LastMarginUpdateDate { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime UpdatedUtc { get; set; }
    }

    [Index(nameof(LevelCode), IsUnique = true)]
    public class DimLevel
    {
        [Key] public uint LevelId { get; set; }
        [Required, MaxLength(64)] public string LevelCode { get; set; } = null!;
        [Required, MaxLength(128)] public string LevelName { get; set; } = null!;
        public ushort LevelOrder { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime UpdatedUtc { get; set; }
    }

    [Index(nameof(NormalizedName), IsUnique = true)]
    public class DimEmployee
    {
        [Key] public ulong EmployeeId { get; set; }
        [MaxLength(64)] public string? EmployeeCode { get; set; }
        [Required, MaxLength(255)] public string FullName { get; set; } = null!;
        [Required, MaxLength(255)] public string NormalizedName { get; set; } = null!;
        public DateTime CreatedUtc { get; set; }
        public DateTime UpdatedUtc { get; set; }
    }

    public class MapEmployeeAlias
    {
        [Key] public ulong EmployeeAliasId { get; set; }
        [Required] public ushort SourceSystemId { get; set; }
        [Required, MaxLength(255)] public string RawName { get; set; } = null!;
        [Required, MaxLength(255)] public string NormalizedRaw { get; set; } = null!;
        [Required] public ulong EmployeeId { get; set; }
        public DateTime CreatedUtc { get; set; }

        public DimSourceSystem? SourceSystem { get; set; }
        public DimEmployee? Employee { get; set; }
    }

    public class MapLevelAlias
    {
        [Key] public ulong LevelAliasId { get; set; }
        [Required] public ushort SourceSystemId { get; set; }
        [Required, MaxLength(128)] public string RawLevel { get; set; } = null!;
        [Required, MaxLength(128)] public string NormalizedRaw { get; set; } = null!;
        [Required] public uint LevelId { get; set; }
        public DateTime CreatedUtc { get; set; }

        public DimSourceSystem? SourceSystem { get; set; }
        public DimLevel? Level { get; set; }
    }

    public class FactPlanByLevel
    {
        [Key] public ulong PlanId { get; set; }
        [Required] public DateTime LoadUtc { get; set; }
        [Required] public ushort SourceSystemId { get; set; }
        [Required] public ushort MeasurementPeriodId { get; set; }
        [Required, MaxLength(64)] public string EngagementId { get; set; } = null!;
        [Required] public uint LevelId { get; set; }
        [Required, Column(TypeName = "decimal(12,2)")] public decimal PlannedHours { get; set; }
        [Column(TypeName = "decimal(14,4)")] public decimal? PlannedRate { get; set; }
        public DateTime CreatedUtc { get; set; }
    }

    public class FactEtcSnapshot
    {
        [Key] public ulong EtcId { get; set; }
        [Required, MaxLength(100)] public string SnapshotLabel { get; set; } = null!;
        [Required] public DateTime LoadUtc { get; set; }
        [Required] public ushort SourceSystemId { get; set; }
        [Required] public ushort MeasurementPeriodId { get; set; }
        [Required, MaxLength(64)] public string EngagementId { get; set; } = null!;
        [Required] public ulong EmployeeId { get; set; }
        public uint? LevelId { get; set; }
        [Required, Column(TypeName = "decimal(12,2)")] public decimal HoursIncurred { get; set; }
        [Required, Column(TypeName = "decimal(12,2)")] public decimal EtcRemaining { get; set; }
        public DateTime CreatedUtc { get; set; }
    }

    public class FactEngagementMargin
    {
        [Required] public ushort MeasurementPeriodId { get; set; }
        [Required, MaxLength(64)] public string EngagementId { get; set; } = null!;
        [Required, Column(TypeName = "decimal(6,3)")] public decimal MarginValue { get; set; }
    }

    public class FactDeclaredErpWeek
    {
        [Key] public ulong ErpId { get; set; }
        [Required] public ushort SourceSystemId { get; set; }
        [Required] public ushort MeasurementPeriodId { get; set; }
        [Required] public DateOnly WeekStartDate { get; set; }
        [Required, MaxLength(64)] public string EngagementId { get; set; } = null!;
        [Required] public ulong EmployeeId { get; set; }
        [Required, Column(TypeName = "decimal(12,2)")] public decimal DeclaredHours { get; set; }
        [Required] public DateTime LoadUtc { get; set; }
        public DateTime CreatedUtc { get; set; }
    }

    public class FactDeclaredRetainWeek
    {
        [Key] public ulong RetainId { get; set; }
        [Required] public ushort SourceSystemId { get; set; }
        [Required] public ushort MeasurementPeriodId { get; set; }
        [Required] public DateOnly WeekStartDate { get; set; }
        [Required, MaxLength(64)] public string EngagementId { get; set; } = null!;
        [Required] public ulong EmployeeId { get; set; }
        [Required, Column(TypeName = "decimal(12,2)")] public decimal DeclaredHours { get; set; }
        [Required] public DateTime LoadUtc { get; set; }
        public DateTime CreatedUtc { get; set; }
    }

    public class FactTimesheetCharge
    {
        [Key] public ulong ChargeId { get; set; }
        [Required] public ushort SourceSystemId { get; set; }
        [Required] public ushort MeasurementPeriodId { get; set; }
        [Required] public DateOnly ChargeDate { get; set; }
        [Required, MaxLength(64)] public string EngagementId { get; set; } = null!;
        [Required] public ulong EmployeeId { get; set; }
        [Required, Column(TypeName = "decimal(12,2)")] public decimal HoursCharged { get; set; }
        [Column(TypeName = "decimal(14,4)")] public decimal? CostAmount { get; set; }
        [Required] public DateTime LoadUtc { get; set; }
        public DateTime CreatedUtc { get; set; }
    }

    public class AuditEtcVsCharges
    {
        [Key] public ulong AuditId { get; set; }
        [Required, MaxLength(100)] public string SnapshotLabel { get; set; } = null!;
        [Required] public ushort MeasurementPeriodId { get; set; }
        [Required, MaxLength(64)] public string EngagementId { get; set; } = null!;
        [Required] public ulong EmployeeId { get; set; }
        [Required] public DateOnly LastWeekEndDate { get; set; }
        [Required, Column(TypeName = "decimal(12,2)")] public decimal EtcHoursIncurred { get; set; }
        [Required, Column(TypeName = "decimal(12,2)")] public decimal ChargesSumHours { get; set; }
        [Required, Column(TypeName = "decimal(12,2)")] public decimal DiffHours { get; set; }
        public DateTime CreatedUtc { get; set; }
    }

    [Keyless]
    public class VwChargesSum
    {
        [Required, MaxLength(64)] public string EngagementId { get; set; } = null!;
        [Required] public ulong EmployeeId { get; set; }
        [Required] public DateOnly ChargeDate { get; set; }
        [Column(TypeName = "decimal(34,2)")] public decimal? HoursCharged { get; set; }
    }

    [Keyless]
    public class VwLatestEtcPerEmployee
    {
        [Required] public ulong EtcId { get; set; }
        [Required, MaxLength(100)] public string SnapshotLabel { get; set; } = null!;
        [Required] public DateTime LoadUtc { get; set; }
        [Required] public ushort SourceSystemId { get; set; }
        [Required, MaxLength(64)] public string EngagementId { get; set; } = null!;
        [Required] public ulong EmployeeId { get; set; }
        public uint? LevelId { get; set; }
        [Required, Column(TypeName = "decimal(12,2)")] public decimal HoursIncurred { get; set; }
        [Required, Column(TypeName = "decimal(12,2)")] public decimal EtcRemaining { get; set; }
        public DateTime CreatedUtc { get; set; }
    }

    [Keyless]
    public class VwPlanVsActualByLevel
    {
        [Required, MaxLength(64)] public string EngagementId { get; set; } = null!;
        [Required] public uint LevelId { get; set; }
        [Column(TypeName = "decimal(34,2)")] public decimal? PlannedHours { get; set; }
        [Column(TypeName = "decimal(56,2)")] public decimal? ActualHours { get; set; }
    }

    public class AppDbContext : DbContext
    {
        public DbSet<DimSourceSystem> DimSourceSystems => Set<DimSourceSystem>();
        public DbSet<DimMeasurementPeriod> DimMeasurementPeriods => Set<DimMeasurementPeriod>();
        public DbSet<DimFiscalYear> DimFiscalYears => Set<DimFiscalYear>();
        public DbSet<DimEngagement> DimEngagements => Set<DimEngagement>();
        public DbSet<DimLevel> DimLevels => Set<DimLevel>();
        public DbSet<DimEmployee> DimEmployees => Set<DimEmployee>();
        public DbSet<MapEmployeeAlias> MapEmployeeAliases => Set<MapEmployeeAlias>();
        public DbSet<MapLevelAlias> MapLevelAliases => Set<MapLevelAlias>();
        public DbSet<FactPlanByLevel> FactPlanByLevels => Set<FactPlanByLevel>();
        public DbSet<FactEtcSnapshot> FactEtcSnapshots => Set<FactEtcSnapshot>();
        public DbSet<FactEngagementMargin> FactEngagementMargins => Set<FactEngagementMargin>();
        public DbSet<FactDeclaredErpWeek> FactDeclaredErpWeeks => Set<FactDeclaredErpWeek>();
        public DbSet<FactDeclaredRetainWeek> FactDeclaredRetainWeeks => Set<FactDeclaredRetainWeek>();
        public DbSet<FactTimesheetCharge> FactTimesheetCharges => Set<FactTimesheetCharge>();
        public DbSet<AuditEtcVsCharges> AuditEtcVsCharges => Set<AuditEtcVsCharges>();
        public DbSet<VwChargesSum> VwChargesSum => Set<VwChargesSum>();
        public DbSet<VwLatestEtcPerEmployee> VwLatestEtcPerEmployees => Set<VwLatestEtcPerEmployee>();
        public DbSet<VwPlanVsActualByLevel> VwPlanVsActualByLevels => Set<VwPlanVsActualByLevel>();

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<DimMeasurementPeriod>(entity =>
            {
                entity.Property(e => e.Description).HasMaxLength(100);
                entity.Property(e => e.StartDate).HasColumnType("date");
                entity.Property(e => e.EndDate).HasColumnType("date");
                entity.Property(e => e.CreatedUtc)
                    .HasColumnType("datetime(6)")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
                    .ValueGeneratedOnAdd();
                entity.Property(e => e.UpdatedUtc)
                    .HasColumnType("datetime(6)")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
                    .ValueGeneratedOnAddOrUpdate();
            });

            modelBuilder.Entity<DimFiscalYear>(entity =>
            {
                entity.Property(e => e.Description).HasMaxLength(100);
                entity.Property(e => e.DateFrom).HasColumnType("date");
                entity.Property(e => e.DateTo).HasColumnType("date");
                entity.Property(e => e.CreatedUtc)
                    .HasColumnType("datetime(6)")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
                    .ValueGeneratedOnAdd();
                entity.Property(e => e.UpdatedUtc)
                    .HasColumnType("datetime(6)")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
                    .ValueGeneratedOnAddOrUpdate();
            });

            modelBuilder.Entity<DimEngagement>(entity =>
            {
                entity.Property(e => e.CreatedUtc)
                    .HasColumnType("datetime(6)")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
                    .ValueGeneratedOnAdd();
                entity.Property(e => e.UpdatedUtc)
                    .HasColumnType("datetime(6)")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
                    .ValueGeneratedOnAddOrUpdate();
                entity.Property(e => e.LastMarginUpdateDate)
                    .HasColumnType("datetime(6)");
            });

            modelBuilder.Entity<DimLevel>(entity =>
            {
                entity.Property(e => e.CreatedUtc)
                    .HasColumnType("datetime(6)")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
                    .ValueGeneratedOnAdd();
                entity.Property(e => e.UpdatedUtc)
                    .HasColumnType("datetime(6)")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
                    .ValueGeneratedOnAddOrUpdate();
            });

            modelBuilder.Entity<DimEmployee>(entity =>
            {
                entity.Property(e => e.CreatedUtc)
                    .HasColumnType("datetime(6)")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
                    .ValueGeneratedOnAdd();
                entity.Property(e => e.UpdatedUtc)
                    .HasColumnType("datetime(6)")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
                    .ValueGeneratedOnAddOrUpdate();
            });

            modelBuilder.Entity<MapEmployeeAlias>(entity =>
            {
                entity.Property(e => e.CreatedUtc)
                    .HasColumnType("datetime(6)")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
                    .ValueGeneratedOnAdd();
            });

            modelBuilder.Entity<MapLevelAlias>(entity =>
            {
                entity.Property(e => e.CreatedUtc)
                    .HasColumnType("datetime(6)")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
                    .ValueGeneratedOnAdd();
            });

            modelBuilder.Entity<FactPlanByLevel>(entity =>
            {
                entity.Property(e => e.LoadUtc).HasColumnType("datetime(6)");
                entity.Property(e => e.CreatedUtc)
                    .HasColumnType("datetime(6)")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
                    .ValueGeneratedOnAdd();
                entity.Property(e => e.PlannedHours).HasPrecision(12, 2);
                entity.Property(e => e.PlannedRate).HasPrecision(14, 4);
            });

            modelBuilder.Entity<FactEtcSnapshot>(entity =>
            {
                entity.Property(e => e.LoadUtc).HasColumnType("datetime(6)");
                entity.Property(e => e.CreatedUtc)
                    .HasColumnType("datetime(6)")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
                    .ValueGeneratedOnAdd();
                entity.Property(e => e.HoursIncurred).HasPrecision(12, 2);
                entity.Property(e => e.EtcRemaining).HasPrecision(12, 2);
            });

            modelBuilder.Entity<FactEngagementMargin>(entity =>
            {
                entity.HasKey(e => new { e.MeasurementPeriodId, e.EngagementId });
                entity.Property(e => e.MarginValue).HasPrecision(6, 3);
            });

            modelBuilder.Entity<FactDeclaredErpWeek>(entity =>
            {
                entity.Property(e => e.LoadUtc).HasColumnType("datetime(6)");
                entity.Property(e => e.CreatedUtc)
                    .HasColumnType("datetime(6)")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
                    .ValueGeneratedOnAdd();
                entity.Property(e => e.DeclaredHours).HasPrecision(12, 2);
            });

            modelBuilder.Entity<FactDeclaredRetainWeek>(entity =>
            {
                entity.Property(e => e.LoadUtc).HasColumnType("datetime(6)");
                entity.Property(e => e.CreatedUtc)
                    .HasColumnType("datetime(6)")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
                    .ValueGeneratedOnAdd();
                entity.Property(e => e.DeclaredHours).HasPrecision(12, 2);
            });

            modelBuilder.Entity<FactTimesheetCharge>(entity =>
            {
                entity.Property(e => e.LoadUtc).HasColumnType("datetime(6)");
                entity.Property(e => e.CreatedUtc)
                    .HasColumnType("datetime(6)")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
                    .ValueGeneratedOnAdd();
                entity.Property(e => e.HoursCharged).HasPrecision(12, 2);
                entity.Property(e => e.CostAmount).HasPrecision(14, 4);
            });

            modelBuilder.Entity<AuditEtcVsCharges>(entity =>
            {
                entity.Property(e => e.CreatedUtc)
                    .HasColumnType("datetime(6)")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
                    .ValueGeneratedOnAdd();
                entity.Property(e => e.EtcHoursIncurred).HasPrecision(12, 2);
                entity.Property(e => e.ChargesSumHours).HasPrecision(12, 2);
                entity.Property(e => e.DiffHours).HasPrecision(12, 2);
            });

            modelBuilder.Entity<VwChargesSum>(entity =>
            {
                entity.ToView("vw_charges_sum");
                entity.Property(e => e.ChargeDate).HasColumnType("date");
                entity.Property(e => e.HoursCharged).HasPrecision(34, 2);
            });

            modelBuilder.Entity<VwLatestEtcPerEmployee>(entity =>
            {
                entity.ToView("vw_latest_etc_per_employee");
                entity.Property(e => e.LoadUtc).HasColumnType("datetime(6)");
                entity.Property(e => e.CreatedUtc).HasColumnType("datetime(6)");
                entity.Property(e => e.HoursIncurred).HasPrecision(12, 2);
                entity.Property(e => e.EtcRemaining).HasPrecision(12, 2);
            });

            modelBuilder.Entity<VwPlanVsActualByLevel>(entity =>
            {
                entity.ToView("vw_plan_vs_actual_by_level");
                entity.Property(e => e.PlannedHours).HasPrecision(34, 2);
                entity.Property(e => e.ActualHours).HasPrecision(56, 2);
            });
        }
    }
}
