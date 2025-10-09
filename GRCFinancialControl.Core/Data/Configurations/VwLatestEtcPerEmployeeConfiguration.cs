using GRCFinancialControl.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GRCFinancialControl.Data.Configurations;

public class VwLatestEtcPerEmployeeConfiguration : IEntityTypeConfiguration<VwLatestEtcPerEmployee>
{
    public void Configure(EntityTypeBuilder<VwLatestEtcPerEmployee> builder)
    {
        builder.ToView("VwLatestEtcPerEmployee");

        builder.HasNoKey();

        builder.Property(e => e.EtcId)
            .HasColumnName("EtcId")
            .HasColumnType("bigint(20)")
            .IsRequired();

        builder.Property(e => e.SnapshotLabel)
            .HasColumnName("SnapshotLabel")
            .HasColumnType("varchar(100)")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.LoadUtc)
            .HasColumnName("LoadUtc")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.Property(e => e.SourceSystemId)
            .HasColumnName("SourceSystemId")
            .HasColumnType("bigint(20)")
            .IsRequired();

        builder.Property(e => e.EngagementId)
            .HasColumnName("EngagementId")
            .HasColumnType("varchar(64)")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(e => e.EmployeeId)
            .HasColumnName("EmployeeId")
            .HasColumnType("bigint(20)")
            .IsRequired();

        builder.Property(e => e.LevelId)
            .HasColumnName("LevelId")
            .HasColumnType("bigint(20)");

        builder.Property(e => e.HoursIncurred)
            .HasColumnName("HoursIncurred")
            .HasColumnType("decimal(12,2)")
            .IsRequired();

        builder.Property(e => e.EtcRemaining)
            .HasColumnName("EtcRemaining")
            .HasColumnType("decimal(12,2)")
            .IsRequired();

        builder.Property(e => e.CreatedUtc)
            .HasColumnName("CreatedUtc")
            .HasColumnType("datetime(6)")
            .IsRequired();
    }
}
