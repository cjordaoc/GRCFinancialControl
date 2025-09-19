using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GRCFinancialControl.Data
{
    public class ParameterEntry
    {
        [Key]
        [MaxLength(128)]
        [Column("param_key")]
        public string Key { get; set; } = string.Empty;

        [Required]
        [Column("param_value")]
        public string Value { get; set; } = string.Empty;

        [Column("updated_utc")]
        public DateTime UpdatedUtc { get; set; }
    }

    public class LocalSqliteContext : DbContext
    {
        public LocalSqliteContext(DbContextOptions<LocalSqliteContext> options) : base(options)
        {
        }

        public DbSet<ParameterEntry> Parameters => Set<ParameterEntry>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ParameterEntry>(entity =>
            {
                entity.ToTable("parameters");
                entity.Property(e => e.Key).HasMaxLength(128);
                entity.Property(e => e.Value).IsRequired();
                entity.Property(e => e.UpdatedUtc)
                    .HasColumnType("TEXT")
                    .HasDefaultValueSql("datetime('now')");
            });
        }
    }
}
