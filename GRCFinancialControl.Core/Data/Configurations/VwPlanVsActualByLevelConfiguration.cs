using GRCFinancialControl.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GRCFinancialControl.Data.Configurations;

public class VwPlanVsActualByLevelConfiguration : IEntityTypeConfiguration<VwPlanVsActualByLevel>
{
    public void Configure(EntityTypeBuilder<VwPlanVsActualByLevel> builder)
    {
        builder.ToView("VwPlanVsActualByLevel");

        builder.HasNoKey();

        builder.Property(e => e.EngagementId)
            .HasColumnName("EngagementId")
            .HasColumnType("varchar(64)")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(e => e.LevelId)
            .HasColumnName("LevelId")
            .HasColumnType("bigint(20)")
            .IsRequired();

        builder.Property(e => e.PlannedHours)
            .HasColumnName("PlannedHours")
            .HasColumnType("decimal(34,2)");

        builder.Property(e => e.ActualHours)
            .HasColumnName("ActualHours")
            .HasColumnType("decimal(56,2)");
    }
}
