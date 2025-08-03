using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Contracts;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class CompetitionCompetitorLineScore : CanonicalEntityBase<Guid>, IHasExternalIds
    {
        public Guid CompetitionCompetitorId { get; set; }

        public int Period { get; set; }

        public double Value { get; set; }

        public required string DisplayValue { get; set; }

        public required string SourceId { get; set; } = null!;

        public required string SourceDescription { get; set; } = null!;

        public string? SourceState { get; set; }

        public CompetitionCompetitor CompetitionCompetitor { get; set; } = null!;

        public ICollection<CompetitionCompetitorLineScoreExternalId> ExternalIds { get; set; } = [];

        public IEnumerable<ExternalId> GetExternalIds() => ExternalIds;

        public class EntityConfiguration : IEntityTypeConfiguration<CompetitionCompetitorLineScore>
        {
            public void Configure(EntityTypeBuilder<CompetitionCompetitorLineScore> builder)
            {
                builder.ToTable(nameof(CompetitionCompetitorLineScore));

                builder.HasKey(x => x.Id);

                builder.Property(x => x.DisplayValue)
                    .HasMaxLength(20);

                builder.Property(x => x.SourceId)
                    .HasMaxLength(10);

                builder.Property(x => x.SourceDescription)
                    .HasMaxLength(100);

                builder.Property(x => x.SourceState)
                    .HasMaxLength(20);

                builder.HasOne(x => x.CompetitionCompetitor)
                    .WithMany(x => x.LineScores)
                    .HasForeignKey(x => x.CompetitionCompetitorId)
                    .OnDelete(DeleteBehavior.Cascade);
            }
        }
    }
}