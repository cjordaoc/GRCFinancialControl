using GRCFinancialControl.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GRCFinancialControl.Data.Configurations;

public class DimFiscalYearConfiguration : IEntityTypeConfiguration<DimFiscalYear>
{
    public void Configure(EntityTypeBuilder<DimFiscalYear> builder)
    {
        builder.ToTable("DimFiscalYears");

        builder.HasKey(e => e.FiscalYearId);

        builder.Property(e => e.FiscalYearId)
            .HasColumnName("FiscalYearId")
            .HasColumnType("bigint(20)")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.Description)
            .HasColumnName("Description")
            .HasColumnType("varchar(100)")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.DateFrom)
            .HasColumnName("DateFrom")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(e => e.DateTo)
            .HasColumnName("DateTo")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(e => e.IsActive)
            .HasColumnName("IsActive")
            .HasColumnType("tinyint(1)")
            .HasDefaultValue(false)
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
