using GRCFinancialControl.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GRCFinancialControl.Data.Configurations;

public class DimEmployeeConfiguration : IEntityTypeConfiguration<DimEmployee>
{
    public void Configure(EntityTypeBuilder<DimEmployee> builder)
    {
        builder.ToTable("DimEmployees");

        builder.HasKey(e => e.EmployeeId);

        builder.HasIndex(e => e.NormalizedName).IsUnique();

        builder.Property(e => e.EmployeeId)
            .HasColumnName("EmployeeId")
            .HasColumnType("bigint(20)")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.EmployeeCode)
            .HasColumnName("EmployeeCode")
            .HasColumnType("varchar(64)")
            .HasMaxLength(64);

        builder.Property(e => e.FullName)
            .HasColumnName("FullName")
            .HasColumnType("varchar(255)")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(e => e.NormalizedName)
            .HasColumnName("NormalizedName")
            .HasColumnType("varchar(255)")
            .HasMaxLength(255)
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
