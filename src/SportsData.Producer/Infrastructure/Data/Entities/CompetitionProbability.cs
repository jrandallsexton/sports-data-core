using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Contracts;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class CompetitionProbability : CanonicalEntityBase<Guid>, IHasExternalIds
    {
        public Guid CompetitionId { get; set; }
        public Competition Competition { get; set; } = null!;

        public Guid? PlayId { get; set; }
        public Play? Play { get; set; }

        public double TiePercentage { get; set; }
        public double HomeWinPercentage { get; set; }
        public double AwayWinPercentage { get; set; }

        public int SecondsLeft { get; set; }

        public string LastModifiedRaw { get; set; } = null!;
        public string SequenceNumber { get; set; } = null!;

        public string SourceId { get; set; } = null!;
        public string SourceDescription { get; set; } = null!;
        public string SourceState { get; set; } = null!;


        public ICollection<CompetitionProbabilityExternalId> ExternalIds { get; set; } = [];

        public IEnumerable<ExternalId> GetExternalIds() => ExternalIds;

        public class EntityConfiguration : IEntityTypeConfiguration<CompetitionProbability>
        {
            public void Configure(EntityTypeBuilder<CompetitionProbability> builder)
            {
                builder.ToTable("CompetitionProbability");

                builder.HasKey(x => x.Id);

                builder.HasOne(x => x.Competition)
                    .WithMany(x => x.Probabilities)
                    .HasForeignKey(x => x.CompetitionId)
                    .OnDelete(DeleteBehavior.Cascade);

                builder.HasOne(x => x.Play)
                    .WithMany(x => x.Probabilities)
                    .HasForeignKey(x => x.PlayId)
                    .OnDelete(DeleteBehavior.Restrict);

                builder.Property(x => x.TiePercentage).HasPrecision(5, 2);
                builder.Property(x => x.HomeWinPercentage).HasPrecision(5, 2);
                builder.Property(x => x.AwayWinPercentage).HasPrecision(5, 2);

                builder.Property(x => x.SecondsLeft);

                builder.Property(x => x.LastModifiedRaw)
                    .HasMaxLength(64);

                builder.Property(x => x.SequenceNumber)
                    .HasMaxLength(64);

                builder.Property(x => x.SourceId)
                    .HasMaxLength(64);

                builder.Property(x => x.SourceDescription)
                    .HasMaxLength(128);

                builder.Property(x => x.SourceState)
                    .HasMaxLength(64);
            }
        }
    }
}