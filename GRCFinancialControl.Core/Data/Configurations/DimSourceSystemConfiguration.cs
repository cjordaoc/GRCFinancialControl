using GRCFinancialControl.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GRCFinancialControl.Data.Configurations;

public class DimSourceSystemConfiguration : IEntityTypeConfiguration<DimSourceSystem>
{
    public void Configure(EntityTypeBuilder<DimSourceSystem> builder)
    {
        builder.ToTable("DimSourceSystems");

        builder.HasKey(e => e.SourceSystemId);

        builder.HasIndex(e => e.SystemCode).IsUnique();

        builder.Property(e => e.SourceSystemId)
            .HasColumnName("SourceSystemId")
            .HasColumnType("bigint(20)")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.SystemCode)
            .HasColumnName("SystemCode")
            .HasColumnType("varchar(50)")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.SystemName)
            .HasColumnName("SystemName")
            .HasColumnType("varchar(100)")
            .HasMaxLength(100)
            .IsRequired();
    }
}
