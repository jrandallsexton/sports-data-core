using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities;

public class SeasonWeekExternalId : ExternalId
{
    public Guid SeasonWeekId { get; set; }

    public SeasonWeek? SeasonWeek { get; set; }

    public class EntityConfiguration : IEntityTypeConfiguration<SeasonWeekExternalId>
    {
        public void Configure(EntityTypeBuilder<SeasonWeekExternalId> builder)
        {
            builder.ToTable(nameof(SeasonWeekExternalId));
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Id).ValueGeneratedNever();
            builder.Property(e => e.Value)
                .HasMaxLength(100)
                .IsRequired();
            builder.Property(e => e.SourceUrlHash)
                .HasMaxLength(256)
                .IsRequired();
            builder.Property(e => e.Provider)
                .IsRequired();
            builder.HasOne(e => e.SeasonWeek)
                .WithMany(p => p.ExternalIds)
                .HasForeignKey(e => e.SeasonWeekId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}