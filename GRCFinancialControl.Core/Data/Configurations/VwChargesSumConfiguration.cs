using GRCFinancialControl.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GRCFinancialControl.Data.Configurations;

public class VwChargesSumConfiguration : IEntityTypeConfiguration<VwChargesSum>
{
    public void Configure(EntityTypeBuilder<VwChargesSum> builder)
    {
        builder.ToView("VwChargesSum");

        builder.HasNoKey();

        builder.Property(e => e.EngagementId)
            .HasColumnName("EngagementId")
            .HasColumnType("varchar(64)")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(e => e.EmployeeId)
            .HasColumnName("EmployeeId")
            .HasColumnType("bigint(20)")
            .IsRequired();

        builder.Property(e => e.ChargeDate)
            .HasColumnName("ChargeDate")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(e => e.HoursCharged)
            .HasColumnName("HoursCharged")
            .HasColumnType("decimal(34,2)");
    }
}
