using GRCFinancialControl.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GRCFinancialControl.Data.Configurations;

public class FactPlanByLevelConfiguration : IEntityTypeConfiguration<FactPlanByLevel>
{
    public void Configure(EntityTypeBuilder<FactPlanByLevel> builder)
    {
        builder.ToTable("FactPlanByLevels");

        builder.HasKey(e => e.PlanId);

        builder.HasIndex(e => e.SourceSystemId);
        builder.HasIndex(e => e.MeasurementPeriodId);
        builder.HasIndex(e => e.EngagementId);
        builder.HasIndex(e => e.LevelId);

        builder.Property(e => e.PlanId)
            .HasColumnName("PlanId")
            .HasColumnType("bigint(20)")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.LoadUtc)
            .HasColumnName("LoadUtc")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.Property(e => e.SourceSystemId)
            .HasColumnName("SourceSystemId")
            .HasColumnType("bigint(20)")
            .IsRequired();

        builder.Property(e => e.MeasurementPeriodId)
            .HasColumnName("MeasurementPeriodId")
            .HasColumnType("bigint(20)")
            .IsRequired();

        builder.Property(e => e.EngagementId)
            .HasColumnName("EngagementId")
            .HasColumnType("varchar(64)")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(e => e.LevelId)
            .HasColumnName("LevelId")
            .HasColumnType("bigint(20)")
            .IsRequired();

        builder.Property(e => e.PlannedHours)
            .HasColumnName("PlannedHours")
            .HasColumnType("decimal(12,2)")
            .IsRequired();

        builder.Property(e => e.PlannedRate)
            .HasColumnName("PlannedRate")
            .HasColumnType("decimal(14,4)");

        builder.Property(e => e.CreatedUtc)
            .HasColumnName("CreatedUtc")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .ValueGeneratedOnAdd();
    }
}
