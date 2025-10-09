using GRCFinancialControl.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GRCFinancialControl.Data.Configurations;

public class MapLevelAliasConfiguration : IEntityTypeConfiguration<MapLevelAlias>
{
    public void Configure(EntityTypeBuilder<MapLevelAlias> builder)
    {
        builder.ToTable("MapLevelAliases");

        builder.HasKey(e => e.LevelAliasId);

        builder.HasIndex(e => e.SourceSystemId);
        builder.HasIndex(e => e.LevelId);

        builder.Property(e => e.LevelAliasId)
            .HasColumnName("LevelAliasId")
            .HasColumnType("bigint(20)")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.SourceSystemId)
            .HasColumnName("SourceSystemId")
            .HasColumnType("bigint(20)")
            .IsRequired();

        builder.Property(e => e.RawLevel)
            .HasColumnName("RawLevel")
            .HasColumnType("varchar(128)")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(e => e.NormalizedRaw)
            .HasColumnName("NormalizedRaw")
            .HasColumnType("varchar(128)")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(e => e.LevelId)
            .HasColumnName("LevelId")
            .HasColumnType("bigint(20)")
            .IsRequired();

        builder.Property(e => e.CreatedUtc)
            .HasColumnName("CreatedUtc")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .ValueGeneratedOnAdd();

        builder.HasOne(e => e.SourceSystem)
            .WithMany(s => s.LevelAliases)
            .HasForeignKey(e => e.SourceSystemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Level)
            .WithMany(l => l.Aliases)
            .HasForeignKey(e => e.LevelId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
