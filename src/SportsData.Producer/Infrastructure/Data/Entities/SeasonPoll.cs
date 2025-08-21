using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Contracts;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class SeasonPoll : CanonicalEntityBase<Guid>, IHasExternalIds
    {
        public required string Name { get; set; }

        public required string ShortName { get; set; }

        public required string Slug { get; set; }

        public required int SeasonYear { get; set; }

        public ICollection<SeasonPollExternalId> ExternalIds { get; set; } = [];

        public IEnumerable<ExternalId> GetExternalIds() => ExternalIds;

        public class EntityConfiguration : IEntityTypeConfiguration<SeasonPoll>
        {
            public void Configure(EntityTypeBuilder<SeasonPoll> builder)
            {
                builder.ToTable(nameof(SeasonPoll));

                builder.Property(t => t.Id)
                    .IsRequired();

                builder.Property(t => t.SeasonYear)
                    .IsRequired();

                builder.Property(t => t.Name)
                    .HasMaxLength(100)
                    .IsRequired();

                builder.Property(t => t.ShortName)
                    .HasMaxLength(100)
                    .IsRequired();

                builder.Property(t => t.Slug)
                    .HasMaxLength(100)
                    .IsRequired();
            }
        }
    }
}
