using GRCFinancialControl.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GRCFinancialControl.Data.Configurations;

public class DimLevelConfiguration : IEntityTypeConfiguration<DimLevel>
{
    public void Configure(EntityTypeBuilder<DimLevel> builder)
    {
        builder.ToTable("DimLevels");

        builder.HasKey(e => e.LevelId);

        builder.HasIndex(e => e.LevelCode).IsUnique();

        builder.Property(e => e.LevelId)
            .HasColumnName("LevelId")
            .HasColumnType("bigint(20)")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.LevelCode)
            .HasColumnName("LevelCode")
            .HasColumnType("varchar(64)")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(e => e.LevelName)
            .HasColumnName("LevelName")
            .HasColumnType("varchar(128)")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(e => e.LevelOrder)
            .HasColumnName("LevelOrder")
            .HasColumnType("smallint(5) unsigned")
            .HasDefaultValue((ushort)0)
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
