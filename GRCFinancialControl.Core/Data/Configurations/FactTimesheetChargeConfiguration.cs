using GRCFinancialControl.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GRCFinancialControl.Data.Configurations;

public class FactTimesheetChargeConfiguration : IEntityTypeConfiguration<FactTimesheetCharge>
{
    public void Configure(EntityTypeBuilder<FactTimesheetCharge> builder)
    {
        builder.ToTable("FactTimesheetCharges");

        builder.HasKey(e => e.ChargeId);

        builder.HasIndex(e => e.SourceSystemId);
        builder.HasIndex(e => e.MeasurementPeriodId);
        builder.HasIndex(e => e.EngagementId);
        builder.HasIndex(e => e.EmployeeId);

        builder.Property(e => e.ChargeId)
            .HasColumnName("ChargeId")
            .HasColumnType("bigint(20)")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.SourceSystemId)
            .HasColumnName("SourceSystemId")
            .HasColumnType("bigint(20)")
            .IsRequired();

        builder.Property(e => e.MeasurementPeriodId)
            .HasColumnName("MeasurementPeriodId")
            .HasColumnType("bigint(20)")
            .IsRequired();

        builder.Property(e => e.ChargeDate)
            .HasColumnName("ChargeDate")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(e => e.EngagementId)
            .HasColumnName("EngagementId")
            .HasColumnType("varchar(64)")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(e => e.EmployeeId)
            .HasColumnName("EmployeeId")
            .HasColumnType("bigint(20)")
            .IsRequired();

        builder.Property(e => e.HoursCharged)
            .HasColumnName("HoursCharged")
            .HasColumnType("decimal(12,2)")
            .IsRequired();

        builder.Property(e => e.CostAmount)
            .HasColumnName("CostAmount")
            .HasColumnType("decimal(14,4)");

        builder.Property(e => e.LoadUtc)
            .HasColumnName("LoadUtc")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.Property(e => e.CreatedUtc)
            .HasColumnName("CreatedUtc")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .ValueGeneratedOnAdd();
    }
}
