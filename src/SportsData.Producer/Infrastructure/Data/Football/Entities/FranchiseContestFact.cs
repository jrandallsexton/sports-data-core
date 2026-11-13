using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Producer.Enums;

namespace SportsData.Producer.Infrastructure.Data.Football.Entities
{
    /// <summary>
    /// One row per team per finalized game: the denormalized team-game shape
    /// War Room splits query ("LSU at night in November vs ranked teams" is a
    /// WHERE + GROUP BY here). A projection, never a source: every column is
    /// derived from Contest, Competition, competitors, odds, box score and
    /// Venue, and a rebuild must reproduce it. See
    /// docs/features/war-room-analytics.md, Phase 2.
    ///
    /// Nulls mean "not known", never zero: a game with no venue zone has no
    /// local fields, a game without a line has no spread, a game without a
    /// box score has no box columns.
    /// </summary>
    public class FranchiseContestFact
    {
        /// <summary>
        /// Bumped when a derivation rule changes, so a rebuild can be verified
        /// (rows still on an old version were not re-projected).
        /// </summary>
        public const int CurrentProjectionVersion = 1;

        // ── Identity ──────────────────────────────────────────────────────
        public Guid ContestId { get; set; }
        public Guid FranchiseSeasonId { get; set; }
        public Guid FranchiseId { get; set; }
        public Guid OpponentFranchiseSeasonId { get; set; }
        public Guid OpponentFranchiseId { get; set; }
        public int SeasonYear { get; set; }
        public int? Week { get; set; }

        /// <summary>SeasonPhase.TypeCode: 1 preseason, 2 regular, 3 postseason.</summary>
        public int? SeasonPhaseTypeCode { get; set; }

        // ── Time ──────────────────────────────────────────────────────────
        public DateTime KickoffUtc { get; set; }

        /// <summary>False when ESPN published a date without a time (Competition.TimeValid).</summary>
        public bool KickoffTimeKnown { get; set; }

        /// <summary>Venue wall-clock kickoff; null without a venue time zone or a known time.</summary>
        public DateTime? KickoffLocal { get; set; }

        /// <summary>Null when KickoffLocal is null: such games are never day or night.</summary>
        public KickoffWindow? KickoffWindow { get; set; }

        /// <summary>1-12, local calendar date (see projector for date-only kickoffs).</summary>
        public int? LocalMonth { get; set; }

        /// <summary>System.DayOfWeek (0 = Sunday), local calendar date.</summary>
        public int? LocalDayOfWeek { get; set; }

        // ── Site ──────────────────────────────────────────────────────────
        public GameSite Site { get; set; }
        public Guid? VenueId { get; set; }

        // ── Grouping ──────────────────────────────────────────────────────
        /// <summary>Conference slug from GroupSeasonMap (e.g. "sec").</summary>
        public string? Conference { get; set; }
        public string? OpponentConference { get; set; }

        /// <summary>Same rule as the conference record (EnrichFranchiseSeasonHandler).</summary>
        public bool IsConferenceGame { get; set; }

        /// <summary>Classification from GroupSeasonMap (e.g. "fbs", "fcs", "d2").</summary>
        public string? Division { get; set; }
        public string? OpponentDivision { get; set; }

        // ── Rank going in (ESPN curatedRank; 99 = unranked, null = unknown) ─
        public int? RankGoingIn { get; set; }
        public int? OpponentRankGoingIn { get; set; }

        // ── Record going in (same season, finalized, preseason excluded) ──
        public int WinsBefore { get; set; }
        public int LossesBefore { get; set; }
        public int TiesBefore { get; set; }
        public int OpponentWinsBefore { get; set; }
        public int OpponentLossesBefore { get; set; }
        public int OpponentTiesBefore { get; set; }

        // ── Result ────────────────────────────────────────────────────────
        public int? PointsFor { get; set; }
        public int? PointsAgainst { get; set; }
        public int? Margin { get; set; }
        public GameResult? Result { get; set; }

        // ── Market ────────────────────────────────────────────────────────
        /// <summary>This team's line (negative = favored), from the primary odds row.</summary>
        public decimal? Spread { get; set; }
        public decimal? OverUnder { get; set; }
        public AtsResult? AtsResult { get; set; }
        public OverUnderResult? TotalResult { get; set; }

        // ── Team box (ESPN team statistics, counts only) ──────────────────
        public int? TotalYards { get; set; }
        public int? PassingYards { get; set; }
        public int? RushingYards { get; set; }
        public int? PassAttempts { get; set; }
        public int? Completions { get; set; }
        public int? RushAttempts { get; set; }
        public int? TotalPlays { get; set; }
        public int? FirstDowns { get; set; }
        public int? ThirdDownConversions { get; set; }
        public int? ThirdDownAttempts { get; set; }
        public int? FourthDownConversions { get; set; }
        public int? FourthDownAttempts { get; set; }
        public int? RedZoneAttempts { get; set; }
        public int? RedZoneScores { get; set; }
        public int? Giveaways { get; set; }
        public int? Takeaways { get; set; }
        public int? Penalties { get; set; }
        public int? PenaltyYards { get; set; }
        public int? PossessionSeconds { get; set; }
        public int? SacksMade { get; set; }
        public int? SacksAllowed { get; set; }

        // ── Bookkeeping ───────────────────────────────────────────────────
        public int ProjectionVersion { get; set; }
        public DateTime ProjectedUtc { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<FranchiseContestFact>
        {
            public void Configure(EntityTypeBuilder<FranchiseContestFact> builder)
            {
                builder.ToTable(nameof(FranchiseContestFact));
                builder.HasKey(x => new { x.ContestId, x.FranchiseSeasonId });

                builder.HasIndex(x => new { x.FranchiseId, x.SeasonYear });
                builder.HasIndex(x => new { x.OpponentFranchiseId, x.SeasonYear });
                builder.HasIndex(x => x.SeasonYear);

                builder.Property(x => x.KickoffLocal).HasColumnType("timestamp without time zone");
                builder.Property(x => x.Conference).HasMaxLength(50);
                builder.Property(x => x.OpponentConference).HasMaxLength(50);
                builder.Property(x => x.Division).HasMaxLength(20);
                builder.Property(x => x.OpponentDivision).HasMaxLength(20);
                builder.Property(x => x.Spread).HasPrecision(5, 1);
                builder.Property(x => x.OverUnder).HasPrecision(5, 1);
            }
        }
    }
}
