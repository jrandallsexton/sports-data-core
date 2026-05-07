using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Baseball.Entities
{
    public class BaseballCompetition : CompetitionBase
    {
        public ICollection<BaseballCompetitionPlay> Plays { get; set; } = [];

        // Sport-specific Status nav typed to the MLB subclass so
        // HalfInning / PeriodPrefix / FeaturedAthletes are reachable
        // without an OfType cast.
        public BaseballCompetitionStatus? Status { get; set; }

        // Series snapshot, populated by BaseballEventCompetitionDocumentProcessor
        // from inline series data on the EventCompetition payload. The
        // snapshot reflects state at-game-start and is locked on first
        // non-null write — subsequent reprocessing does NOT overwrite. This
        // lets historical matchup pages render the state that was true when
        // the game was scheduled, even as ESPN's series state advances.
        // EspnSeriesId is the grouping key (not historical state) and is
        // refreshed every time. See docs/series-snapshot-redesign.md.
        public string? EspnSeriesId { get; set; }

        public string? CurrentSeriesSummary { get; set; }
        public int? CurrentSeriesTotalCompetitions { get; set; }
        public bool? CurrentSeriesCompleted { get; set; }
        public DateTimeOffset? CurrentSeriesStartDate { get; set; }
        public int? CurrentSeriesHomeWins { get; set; }
        public int? CurrentSeriesHomeTies { get; set; }
        public int? CurrentSeriesAwayWins { get; set; }
        public int? CurrentSeriesAwayTies { get; set; }

        public string? SeasonSeriesSummary { get; set; }
        public int? SeasonSeriesTotalCompetitions { get; set; }
        public bool? SeasonSeriesCompleted { get; set; }
        public int? SeasonSeriesHomeWins { get; set; }
        public int? SeasonSeriesHomeTies { get; set; }
        public int? SeasonSeriesAwayWins { get; set; }
        public int? SeasonSeriesAwayTies { get; set; }

        public new class EntityConfiguration : IEntityTypeConfiguration<BaseballCompetition>
        {
            public void Configure(EntityTypeBuilder<BaseballCompetition> builder)
            {
                builder.HasOne<BaseballContest>()
                    .WithMany(x => x.Competitions)
                    .HasForeignKey(x => x.ContestId)
                    .OnDelete(DeleteBehavior.Cascade);

                builder.HasMany(x => x.Plays)
                    .WithOne()
                    .HasForeignKey(x => x.CompetitionId)
                    .OnDelete(DeleteBehavior.Cascade);

                builder.HasOne(x => x.Status)
                    .WithOne()
                    .HasForeignKey<BaseballCompetitionStatus>(x => x.CompetitionId)
                    .OnDelete(DeleteBehavior.Cascade);

                builder.HasIndex(x => x.EspnSeriesId);
            }
        }
    }
}
