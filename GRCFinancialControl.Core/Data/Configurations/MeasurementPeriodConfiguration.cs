using GRCFinancialControl.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GRCFinancialControl.Data.Configurations;

public class MeasurementPeriodConfiguration : IEntityTypeConfiguration<MeasurementPeriod>
{
    public void Configure(EntityTypeBuilder<MeasurementPeriod> builder)
    {
        builder.ToTable("MeasurementPeriods");

        builder.HasKey(e => e.PeriodId);

        builder.HasIndex(e => e.Description);

        builder.Property(e => e.PeriodId)
            .HasColumnName("PeriodId")
            .HasColumnType("bigint(20)")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.Description)
            .HasColumnName("Description")
            .HasColumnType("varchar(255)")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(e => e.StartDate)
            .HasColumnName("StartDate")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(e => e.EndDate)
            .HasColumnName("EndDate")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(e => e.CreatedUtc)
            .HasColumnName("CreatedUtc")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.UpdatedUtc)
            .HasColumnName("UpdatedUtc")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .ValueGeneratedOnAddOrUpdate();
    }
}
