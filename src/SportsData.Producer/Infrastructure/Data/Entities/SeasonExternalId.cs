using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class SeasonExternalId : ExternalId
    {
        public Guid SeasonId { get; set; }

        public Season Season { get; set; } = null!;

        public class EntityConfiguration : IEntityTypeConfiguration<SeasonExternalId>
        {
            public void Configure(EntityTypeBuilder<SeasonExternalId> builder)
            {
                builder.ToTable(nameof(SeasonExternalId));

                builder.HasKey(e => e.Id);
                builder.Property(e => e.Id).ValueGeneratedNever();

                builder.Property(e => e.SeasonId)
                    .IsRequired();

                builder.HasOne(e => e.Season)
                    .WithMany(s => s.ExternalIds)
                    .HasForeignKey(e => e.SeasonId)
                    .OnDelete(DeleteBehavior.Cascade);
            }

        }
    }
}
