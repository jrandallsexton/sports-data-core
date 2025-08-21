using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities;

public class SeasonPollExternalId : ExternalId
{
    public Guid SeasonPollId { get; set; }

    public SeasonPoll Poll { get; set; } = null!;

    public class EntityConfiguration : IEntityTypeConfiguration<SeasonPollExternalId>
    {
        public void Configure(EntityTypeBuilder<SeasonPollExternalId> builder)
        {
            builder.ToTable(nameof(SeasonPollExternalId));

            builder.HasKey(e => e.Id);
            builder.Property(e => e.Id).ValueGeneratedNever();

            builder.Property(e => e.SeasonPollId)
                .IsRequired();

            builder.HasOne(e => e.Poll)
                .WithMany(s => s.ExternalIds)
                .HasForeignKey(e => e.SeasonPollId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}