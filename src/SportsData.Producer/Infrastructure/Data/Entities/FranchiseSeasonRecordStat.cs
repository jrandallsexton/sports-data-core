using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using System.ComponentModel.DataAnnotations.Schema;

namespace SportsData.Producer.Infrastructure.Data.Entities;

public class FranchiseSeasonRecordStat
{
    public Guid Id { get; set; }

    public Guid FranchiseSeasonRecordId { get; set; }

    public required string Name { get; set; }

    public required string DisplayName { get; set; }

    public required string ShortDisplayName { get; set; }

    public required string Description { get; set; }

    public required string Abbreviation { get; set; }

    public required string Type { get; set; }

    public double Value { get; set; }

    public required string DisplayValue { get; set; }

    [ForeignKey(nameof(FranchiseSeasonRecordId))]
    public FranchiseSeasonRecord Record { get; set; } = null!;

    public class EntityConfiguration : IEntityTypeConfiguration<FranchiseSeasonRecordStat>
    {
        public void Configure(EntityTypeBuilder<FranchiseSeasonRecordStat> builder)
        {
            builder.ToTable("FranchiseSeasonRecordStat");
            builder.HasKey(t => t.Id);

            builder.Property(t => t.Name).HasMaxLength(100);
            builder.Property(t => t.DisplayName).HasMaxLength(100);
            builder.Property(t => t.ShortDisplayName).HasMaxLength(50);
            builder.Property(t => t.Description).HasMaxLength(200);
            builder.Property(t => t.Abbreviation).HasMaxLength(20);
            builder.Property(t => t.Type).HasMaxLength(50);
            builder.Property(t => t.DisplayValue).HasMaxLength(100);
            builder.Property(t => t.Value).IsRequired();

            builder.HasOne(t => t.Record)
                .WithMany(r => r.Stats)
                .HasForeignKey(t => t.FranchiseSeasonRecordId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

}