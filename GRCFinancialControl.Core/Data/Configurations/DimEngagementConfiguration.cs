using GRCFinancialControl.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GRCFinancialControl.Data.Configurations;

public class DimEngagementConfiguration : IEntityTypeConfiguration<DimEngagement>
{
    public void Configure(EntityTypeBuilder<DimEngagement> builder)
    {
        builder.ToTable("DimEngagements");

        builder.HasKey(e => e.EngagementId);

        builder.Property(e => e.EngagementId)
            .HasColumnName("EngagementId")
            .HasColumnType("varchar(64)")
            .HasMaxLength(64)
            .ValueGeneratedNever();

        builder.Property(e => e.EngagementTitle)
            .HasColumnName("EngagementTitle")
            .HasColumnType("varchar(255)")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(e => e.IsActive)
            .HasColumnName("IsActive")
            .HasColumnType("tinyint(1)")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(e => e.EngagementPartner)
            .HasColumnName("EngagementPartner")
            .HasColumnType("varchar(255)")
            .HasMaxLength(255);

        builder.Property(e => e.EngagementManager)
            .HasColumnName("EngagementManager")
            .HasColumnType("varchar(255)")
            .HasMaxLength(255);

        builder.Property(e => e.OpeningMargin)
            .HasColumnName("OpeningMargin")
            .HasColumnType("decimal(6,3)")
            .HasDefaultValue(0.000m)
            .IsRequired();

        builder.Property(e => e.CurrentMargin)
            .HasColumnName("CurrentMargin")
            .HasColumnType("double")
            .HasDefaultValue(0d)
            .IsRequired();

        builder.Property(e => e.LastMarginUpdateDate)
            .HasColumnName("LastMarginUpdateDate")
            .HasColumnType("datetime(6)");

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
