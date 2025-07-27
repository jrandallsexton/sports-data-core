using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class Competitor : CanonicalEntityBase<Guid>
    {
        public required Guid FranchiseSeasonId { get; set; }

        public string? Type { get; set; }

        public int Order { get; set; }

        public string? HomeAway { get; set; }

        public bool Winner { get; set; }

        public string? TeamRef { get; set; }

        public int ScoreValue { get; set; } = 0;

        public string ScoreDisplayValue { get; set; } = "0";

        public string? LinescoresRef { get; set; }

        public string? RosterRef { get; set; }

        public string? StatisticsRef { get; set; }

        public string? LeadersRef { get; set; }

        public string? RecordRef { get; set; }

        public string? RanksRef { get; set; }

        public int? CuratedRankCurrent { get; set; }

        public Guid CompetitionId { get; set; } // FK to Competition

        public class EntityConfiguration : IEntityTypeConfiguration<Competitor>
        {
            public void Configure(EntityTypeBuilder<Competitor> builder)
            {
                builder.ToTable(nameof(Competitor));
                builder.HasKey(x => x.Id);

                builder.Property(x => x.Type)
                    .HasMaxLength(20);

                builder.Property(x => x.HomeAway)
                    .HasMaxLength(10);

                builder.Property(x => x.TeamRef)
                    .HasMaxLength(250);

                builder.Property(x => x.LinescoresRef)
                    .HasMaxLength(250);

                builder.Property(x => x.RosterRef)
                    .HasMaxLength(250);

                builder.Property(x => x.StatisticsRef)
                    .HasMaxLength(250);

                builder.Property(x => x.LeadersRef)
                    .HasMaxLength(250);

                builder.Property(x => x.RecordRef)
                    .HasMaxLength(250);

                builder.Property(x => x.RanksRef)
                    .HasMaxLength(250);

                builder.Property(x => x.ScoreDisplayValue)
                    .IsRequired()
                    .HasMaxLength(10); // assuming a small value like "14" or "27-13"

                builder.Property(x => x.CompetitionId)
                    .IsRequired();

                builder.Property(x => x.FranchiseSeasonId)
                    .IsRequired();

                builder.HasOne<Competition>()
                    .WithMany(x => x.Competitors)
                    .HasForeignKey(x => x.CompetitionId)
                    .OnDelete(DeleteBehavior.Cascade);
            }
        }

    }
}
