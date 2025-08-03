using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Contracts;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class CompetitionCompetitorScore : CanonicalEntityBase<Guid>, IHasExternalIds
    {
        public Guid CompetitionCompetitorId { get; set; }

        public CompetitionCompetitor CompetitionCompetitor { get; set; } = null!;

        public double Value { get; set; }

        public string DisplayValue { get; set; } = string.Empty;

        public bool Winner { get; set; }

        public string SourceId { get; set; } = string.Empty;

        public string SourceDescription { get; set; } = string.Empty;

        public ICollection<CompetitionCompetitorScoreExternalId> ExternalIds { get; set; } = [];

        public IEnumerable<ExternalId> GetExternalIds() => ExternalIds;

        public class EntityConfiguration : IEntityTypeConfiguration<CompetitionCompetitorScore>
        {
            public void Configure(EntityTypeBuilder<CompetitionCompetitorScore> builder)
            {
                builder.ToTable(nameof(CompetitionCompetitorScore));
                builder.HasKey(x => x.Id);

                builder.Property(x => x.Value)
                    .IsRequired();

                builder.Property(x => x.DisplayValue)
                    .IsRequired()
                    .HasMaxLength(20);

                builder.Property(x => x.Winner)
                    .IsRequired();

                builder.Property(x => x.SourceId)
                    .IsRequired()
                    .HasMaxLength(50);

                builder.Property(x => x.SourceDescription)
                    .IsRequired()
                    .HasMaxLength(100);

                builder.Property(x => x.CompetitionCompetitorId)
                    .IsRequired();

                builder.HasOne<CompetitionCompetitor>()
                    .WithMany(x => x.Scores)
                    .HasForeignKey(x => x.CompetitionCompetitorId)
                    .OnDelete(DeleteBehavior.Cascade);
            }
        }
    }
}
