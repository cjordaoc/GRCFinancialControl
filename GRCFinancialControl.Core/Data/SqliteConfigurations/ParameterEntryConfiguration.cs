using GRCFinancialControl.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GRCFinancialControl.Data.SqliteConfigurations;

public class ParameterEntryConfiguration : IEntityTypeConfiguration<ParameterEntry>
{
    public void Configure(EntityTypeBuilder<ParameterEntry> builder)
    {
        builder.ToTable("parameters");

        builder.HasKey(e => e.Key);

        builder.Property(e => e.Key)
            .HasColumnName("param_key")
            .HasColumnType("TEXT")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(e => e.Value)
            .HasColumnName("param_value")
            .HasColumnType("TEXT")
            .IsRequired();

        builder.Property(e => e.UpdatedUtc)
            .HasColumnName("updated_utc")
            .HasColumnType("TEXT")
            .HasDefaultValueSql("datetime('now')")
            .ValueGeneratedOnAddOrUpdate();
    }
}
