using GRCFinancialControl.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GRCFinancialControl.Data.Configurations;

public class MapEmployeeCodeConfiguration : IEntityTypeConfiguration<MapEmployeeCode>
{
    public void Configure(EntityTypeBuilder<MapEmployeeCode> builder)
    {
        builder.ToTable("MapEmployeeCodes");

        builder.HasKey(e => e.EmployeeCodeId);

        builder.HasIndex(e => new { e.SourceSystemId, e.EmployeeCode }).IsUnique();
        builder.HasIndex(e => e.EmployeeId);

        builder.Property(e => e.EmployeeCodeId)
            .HasColumnName("EmployeeCodeId")
            .HasColumnType("bigint(20)")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.SourceSystemId)
            .HasColumnName("SourceSystemId")
            .HasColumnType("bigint(20)")
            .IsRequired();

        builder.Property(e => e.EmployeeCode)
            .HasColumnName("EmployeeCode")
            .HasColumnType("varchar(64)")
            .HasMaxLength(64)
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
            .WithMany(s => s.EmployeeCodes)
            .HasForeignKey(e => e.SourceSystemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Employee)
            .WithMany(e => e.SourceSystemCodes)
            .HasForeignKey(e => e.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
