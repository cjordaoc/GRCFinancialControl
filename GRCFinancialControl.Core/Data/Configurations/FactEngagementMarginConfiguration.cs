using GRCFinancialControl.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GRCFinancialControl.Data.Configurations;

public class FactEngagementMarginConfiguration : IEntityTypeConfiguration<FactEngagementMargin>
{
    public void Configure(EntityTypeBuilder<FactEngagementMargin> builder)
    {
        builder.ToTable("FactEngagementMargins");

        builder.HasKey(e => new { e.MeasurementPeriodId, e.EngagementId });

        builder.Property(e => e.MeasurementPeriodId)
            .HasColumnName("MeasurementPeriodId")
            .HasColumnType("bigint(20)")
            .IsRequired();

        builder.Property(e => e.EngagementId)
            .HasColumnName("EngagementId")
            .HasColumnType("varchar(64)")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(e => e.MarginValue)
            .HasColumnName("MarginValue")
            .HasColumnType("decimal(6,3)")
            .IsRequired();
    }
}
