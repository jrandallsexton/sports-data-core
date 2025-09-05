using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities;

public class SeasonPollWeekExternalId : ExternalId
{
    public Guid SeasonPollWeekId { get; set; }

    public SeasonPollWeek Week { get; set; } = null!;

    public class EntityConfiguration : IEntityTypeConfiguration<SeasonPollWeekExternalId>
    {
        public void Configure(EntityTypeBuilder<SeasonPollWeekExternalId> builder)
        {
            builder.ToTable(nameof(SeasonPollWeekExternalId));

            builder.HasKey(e => e.Id);
            builder.Property(e => e.Id).ValueGeneratedNever();

            builder.Property(e => e.SeasonPollWeekId)
                .IsRequired();

            builder.HasOne(e => e.Week)
                .WithMany(s => s.ExternalIds)
                .HasForeignKey(e => e.SeasonPollWeekId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}