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

    public class DimEngagement
    {
        [Key, MaxLength(64)] public string EngagementId { get; set; } = null!;
        [MaxLength(255)] public string? EngagementTitle { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    }

    [Index(nameof(LevelCode), IsUnique = true)]
    public class DimLevel
    {
        [Key] public uint LevelId { get; set; }
        [Required, MaxLength(64)] public string LevelCode { get; set; } = null!;
        [Required, MaxLength(128)] public string LevelName { get; set; } = null!;
        public ushort LevelOrder { get; set; }
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    }

    [Index(nameof(NormalizedName), IsUnique = true)]
    [Index(nameof(EmployeeCode))]
    public class DimEmployee
    {
        [Key] public ulong EmployeeId { get; set; }
        [MaxLength(64)] public string? EmployeeCode { get; set; }
        [Required, MaxLength(255)] public string FullName { get; set; } = null!;
        [Required, MaxLength(255)] public string NormalizedName { get; set; } = null!;
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    }

    [Index(nameof(SourceSystemId), nameof(NormalizedRaw), IsUnique = true)]
    public class MapEmployeeAlias
    {
        [Key] public ulong EmployeeAliasId { get; set; }
        [Required] public ushort SourceSystemId { get; set; }
        [Required, MaxLength(255)] public string RawName { get; set; } = null!;
        [Required, MaxLength(255)] public string NormalizedRaw { get; set; } = null!;
        [Required] public ulong EmployeeId { get; set; }
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        public DimSourceSystem? SourceSystem { get; set; }
        public DimEmployee? Employee { get; set; }
    }

    [Index(nameof(SourceSystemId), nameof(NormalizedRaw), IsUnique = true)]
    public class MapLevelAlias
    {
        [Key] public ulong LevelAliasId { get; set; }
        [Required] public ushort SourceSystemId { get; set; }
        [Required, MaxLength(128)] public string RawLevel { get; set; } = null!;
        [Required, MaxLength(128)] public string NormalizedRaw { get; set; } = null!;
        [Required] public uint LevelId { get; set; }
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        public DimSourceSystem? SourceSystem { get; set; }
        public DimLevel? Level { get; set; }
    }

    [Index(nameof(LoadUtc))]
    [Index(nameof(EngagementId), nameof(LevelId))]
    public class FactPlanByLevel
    {
        [Key] public ulong PlanId { get; set; }
        [Required] public DateTime LoadUtc { get; set; }
        [Required] public ushort SourceSystemId { get; set; }
        [Required, MaxLength(64)] public string EngagementId { get; set; } = null!;
        [Required] public uint LevelId { get; set; }
        [Required, Column(TypeName = "decimal(12,2)")] public decimal PlannedHours { get; set; }
        [Column(TypeName = "decimal(14,4)")] public decimal? PlannedRate { get; set; }
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    }

    [Index(nameof(SnapshotLabel), nameof(LoadUtc))]
    [Index(nameof(EngagementId), nameof(EmployeeId))]
    public class FactEtcSnapshot
    {
        [Key] public ulong EtcId { get; set; }
        [Required, MaxLength(100)] public string SnapshotLabel { get; set; } = null!;
        [Required] public DateTime LoadUtc { get; set; }
        [Required] public ushort SourceSystemId { get; set; }
        [Required, MaxLength(64)] public string EngagementId { get; set; } = null!;
        [Required] public ulong EmployeeId { get; set; }
        public uint? LevelId { get; set; }
        [Required, Column(TypeName = "decimal(12,2)")] public decimal HoursIncurred { get; set; }
        [Required, Column(TypeName = "decimal(12,2)")] public decimal EtcRemaining { get; set; }
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    }

    [Index(nameof(SnapshotLabel), nameof(LoadUtc))]
    [Index(nameof(EngagementId))]
    public class FactEngagementMargin
    {
        [Key] public ulong MarginId { get; set; }
        [Required, MaxLength(100)] public string SnapshotLabel { get; set; } = null!;
        [Required] public DateTime LoadUtc { get; set; }
        [Required] public ushort SourceSystemId { get; set; }
        [Required, MaxLength(64)] public string EngagementId { get; set; } = null!;
        [Required, Column(TypeName = "decimal(6,3)")] public decimal ProjectedMarginPct { get; set; }
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    }

    [Index(nameof(WeekStartDate), nameof(EngagementId), nameof(EmployeeId), IsUnique = true)]
    public class FactDeclaredErpWeek
    {
        [Key] public ulong ErpId { get; set; }
        [Required] public ushort SourceSystemId { get; set; }
        [Required] public DateOnly WeekStartDate { get; set; }
        [Required, MaxLength(64)] public string EngagementId { get; set; } = null!;
        [Required] public ulong EmployeeId { get; set; }
        [Required, Column(TypeName = "decimal(12,2)")] public decimal DeclaredHours { get; set; }
        [Required] public DateTime LoadUtc { get; set; }
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    }

    [Index(nameof(WeekStartDate), nameof(EngagementId), nameof(EmployeeId), IsUnique = true)]
    public class FactDeclaredRetainWeek
    {
        [Key] public ulong RetainId { get; set; }
        [Required] public ushort SourceSystemId { get; set; }
        [Required] public DateOnly WeekStartDate { get; set; }
        [Required, MaxLength(64)] public string EngagementId { get; set; } = null!;
        [Required] public ulong EmployeeId { get; set; }
        [Required, Column(TypeName = "decimal(12,2)")] public decimal DeclaredHours { get; set; }
        [Required] public DateTime LoadUtc { get; set; }
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    }

    [Index(nameof(ChargeDate), nameof(EngagementId), nameof(EmployeeId), IsUnique = true)]
    [Index(nameof(EngagementId), nameof(ChargeDate))]
    [Index(nameof(EmployeeId), nameof(ChargeDate))]
    public class FactTimesheetCharge
    {
        [Key] public ulong ChargeId { get; set; }
        [Required] public ushort SourceSystemId { get; set; }
        [Required] public DateOnly ChargeDate { get; set; }
        [Required, MaxLength(64)] public string EngagementId { get; set; } = null!;
        [Required] public ulong EmployeeId { get; set; }
        [Required, Column(TypeName = "decimal(12,2)")] public decimal HoursCharged { get; set; }
        [Column(TypeName = "decimal(14,4)")] public decimal? CostAmount { get; set; }
        [Required] public DateTime LoadUtc { get; set; }
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    }

    public class AuditEtcVsCharges
    {
        [Key] public ulong AuditId { get; set; }
        [Required, MaxLength(100)] public string SnapshotLabel { get; set; } = null!;
        [Required, MaxLength(64)] public string EngagementId { get; set; } = null!;
        [Required] public ulong EmployeeId { get; set; }
        [Required] public DateOnly LastWeekEndDate { get; set; }
        [Required, Column(TypeName = "decimal(12,2)")] public decimal EtcHoursIncurred { get; set; }
        [Required, Column(TypeName = "decimal(12,2)")] public decimal ChargesSumHours { get; set; }
        [Required, Column(TypeName = "decimal(12,2)")] public decimal DiffHours { get; set; }
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    }

    public class AppDbContext : DbContext
    {
        public DbSet<DimSourceSystem> DimSourceSystems => Set<DimSourceSystem>();
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

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Map decimal precision (Pomelo honors Column attribute, but we ensure defaults)
            modelBuilder.Entity<FactPlanByLevel>().Property(p => p.PlannedHours).HasPrecision(12,2);
            modelBuilder.Entity<FactPlanByLevel>().Property(p => p.PlannedRate).HasPrecision(14,4);

            modelBuilder.Entity<FactEtcSnapshot>().Property(p => p.HoursIncurred).HasPrecision(12,2);
            modelBuilder.Entity<FactEtcSnapshot>().Property(p => p.EtcRemaining).HasPrecision(12,2);

            modelBuilder.Entity<FactEngagementMargin>().Property(p => p.ProjectedMarginPct).HasPrecision(6,3);

            modelBuilder.Entity<FactDeclaredErpWeek>().Property(p => p.DeclaredHours).HasPrecision(12,2);
            modelBuilder.Entity<FactDeclaredRetainWeek>().Property(p => p.DeclaredHours).HasPrecision(12,2);

            modelBuilder.Entity<FactTimesheetCharge>().Property(p => p.HoursCharged).HasPrecision(12,2);
            modelBuilder.Entity<FactTimesheetCharge>().Property(p => p.CostAmount).HasPrecision(14,4);

            modelBuilder.Entity<AuditEtcVsCharges>().Property(p => p.EtcHoursIncurred).HasPrecision(12,2);
            modelBuilder.Entity<AuditEtcVsCharges>().Property(p => p.ChargesSumHours).HasPrecision(12,2);
            modelBuilder.Entity<AuditEtcVsCharges>().Property(p => p.DiffHours).HasPrecision(12,2);
        }
    }
}
