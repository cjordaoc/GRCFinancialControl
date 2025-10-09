using GRCFinancialControl.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GRCFinancialControl.Data.Configurations;

public class FactDeclaredRetainWeekConfiguration : IEntityTypeConfiguration<FactDeclaredRetainWeek>
{
    public void Configure(EntityTypeBuilder<FactDeclaredRetainWeek> builder)
    {
        builder.ToTable("FactDeclaredRetainWeeks");

        builder.HasKey(e => e.RetainId);

        builder.HasIndex(e => e.SourceSystemId);
        builder.HasIndex(e => e.MeasurementPeriodId);
        builder.HasIndex(e => e.EngagementId);
        builder.HasIndex(e => e.EmployeeId);

        builder.Property(e => e.RetainId)
            .HasColumnName("RetainId")
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

        builder.Property(e => e.WeekStartDate)
            .HasColumnName("WeekStartDate")
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

        builder.Property(e => e.DeclaredHours)
            .HasColumnName("DeclaredHours")
            .HasColumnType("decimal(12,2)")
            .IsRequired();

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
