using GRCFinancialControl.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GRCFinancialControl.Data.Configurations;

public class MapEmployeeAliasConfiguration : IEntityTypeConfiguration<MapEmployeeAlias>
{
    public void Configure(EntityTypeBuilder<MapEmployeeAlias> builder)
    {
        builder.ToTable("MapEmployeeAliases");

        builder.HasKey(e => e.EmployeeAliasId);

        builder.HasIndex(e => e.SourceSystemId);
        builder.HasIndex(e => e.EmployeeId);

        builder.Property(e => e.EmployeeAliasId)
            .HasColumnName("EmployeeAliasId")
            .HasColumnType("bigint(20)")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.SourceSystemId)
            .HasColumnName("SourceSystemId")
            .HasColumnType("bigint(20)")
            .IsRequired();

        builder.Property(e => e.RawName)
            .HasColumnName("RawName")
            .HasColumnType("varchar(255)")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(e => e.NormalizedRaw)
            .HasColumnName("NormalizedRaw")
            .HasColumnType("varchar(255)")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(e => e.EmployeeId)
            .HasColumnName("EmployeeId")
            .HasColumnType("bigint(20)")
            .IsRequired();

        builder.Property(e => e.CreatedUtc)
            .HasColumnName("CreatedUtc")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .ValueGeneratedOnAdd();

        builder.HasOne(e => e.SourceSystem)
            .WithMany(s => s.EmployeeAliases)
            .HasForeignKey(e => e.SourceSystemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Employee)
            .WithMany(e => e.Aliases)
            .HasForeignKey(e => e.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
