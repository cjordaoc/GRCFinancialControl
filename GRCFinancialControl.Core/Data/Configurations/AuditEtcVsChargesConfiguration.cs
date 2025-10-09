using GRCFinancialControl.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GRCFinancialControl.Data.Configurations;

public class AuditEtcVsChargesConfiguration : IEntityTypeConfiguration<AuditEtcVsCharges>
{
    public void Configure(EntityTypeBuilder<AuditEtcVsCharges> builder)
    {
        builder.ToTable("AuditEtcVsCharges");

        builder.HasKey(e => e.AuditId);

        builder.HasIndex(e => e.MeasurementPeriodId);
        builder.HasIndex(e => e.EngagementId);
        builder.HasIndex(e => e.EmployeeId);

        builder.Property(e => e.AuditId)
            .HasColumnName("AuditId")
            .HasColumnType("bigint(20)")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.SnapshotLabel)
            .HasColumnName("SnapshotLabel")
            .HasColumnType("varchar(100)")
            .HasMaxLength(100)
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

        builder.Property(e => e.EmployeeId)
            .HasColumnName("EmployeeId")
            .HasColumnType("bigint(20)")
            .IsRequired();

        builder.Property(e => e.LastWeekEndDate)
            .HasColumnName("LastWeekEndDate")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(e => e.EtcHoursIncurred)
            .HasColumnName("EtcHoursIncurred")
            .HasColumnType("decimal(12,2)")
            .IsRequired();

        builder.Property(e => e.ChargesSumHours)
            .HasColumnName("ChargesSumHours")
            .HasColumnType("decimal(12,2)")
            .IsRequired();

        builder.Property(e => e.DiffHours)
            .HasColumnName("DiffHours")
            .HasColumnType("decimal(12,2)")
            .IsRequired();

        builder.Property(e => e.CreatedUtc)
            .HasColumnName("CreatedUtc")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .ValueGeneratedOnAdd();
    }
}
