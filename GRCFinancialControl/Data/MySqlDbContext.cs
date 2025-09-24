using GRCFinancialControl.Data.Configurations;
using Microsoft.EntityFrameworkCore;

namespace GRCFinancialControl.Data;

public class MySqlDbContext : DbContext
{
    public DbSet<DimSourceSystem> DimSourceSystems => Set<DimSourceSystem>();
    public DbSet<MeasurementPeriod> MeasurementPeriods => Set<MeasurementPeriod>();
    public DbSet<DimFiscalYear> DimFiscalYears => Set<DimFiscalYear>();
    public DbSet<DimEngagement> DimEngagements => Set<DimEngagement>();
    public DbSet<DimLevel> DimLevels => Set<DimLevel>();
    public DbSet<DimEmployee> DimEmployees => Set<DimEmployee>();
    public DbSet<MapEmployeeAlias> MapEmployeeAliases => Set<MapEmployeeAlias>();
    public DbSet<MapEmployeeCode> MapEmployeeCodes => Set<MapEmployeeCode>();
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

    public MySqlDbContext(DbContextOptions<MySqlDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(MySqlDbContext).Assembly,
            type => type.Namespace == typeof(DimSourceSystemConfiguration).Namespace);
    }
}
