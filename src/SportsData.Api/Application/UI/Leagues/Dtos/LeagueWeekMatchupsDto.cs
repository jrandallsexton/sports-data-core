using SportsData.Api.Application.UI.Contest.Dtos;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;

using SportsData.Api.Application.Common.Enums;

namespace SportsData.Api.Application.UI.Leagues.Dtos
{
    public class LeagueWeekMatchupsDto
    {
        public int SeasonYear { get; set; }

        public int WeekNumber { get; set; }

        /// <summary>
        /// Inclusive upper bound for the mini-schedule snapshot. Equals
        /// SeasonWeek.EndDate of the displayed week (derived from the first
        /// matchup's contest → SeasonWeek FK). UI passes this to
        /// /ui/teamcard/.../schedule?asOfDate=… so historical pick-review
        /// views show results only through that week's close — fixes both
        /// the MLB "same-week games dropped" bug and the football
        /// "postseason Week 1 sneaks in" bug that a numeric week filter
        /// couldn't cleanly handle. Null when the week has no matchups.
        /// </summary>
        public DateTime? AsOfDate { get; set; }

        public PickType PickType { get; set; }

        public bool UseConfidencePoints { get; set; }

        // Sport enum name ("FootballNcaa", "FootballNfl", "BaseballMlb").
        // UI splits this into url segments (sport/league) via resolveSportLeague().
        public string Sport { get; set; } = default!;

        public List<MatchupForPickDto> Matchups { get; set; } = [];

        public class MatchupForPickDto
        {
            public Guid ContestId { get; set; }

            public string? HeadLine { get; set; }

            public List<ContestPredictionDto> Predictions { get; set; } = [];

            public DateTime StartDateUtc { get; set; }

            /// <summary>
            /// Raw ESPN status type name for programmatic branching
            /// (e.g. "STATUS_IN_PROGRESS", "STATUS_FINAL"). Pair with
            /// <see cref="StatusDescription"/> for display. Null when the
            /// underlying CompetitionStatus row hasn't been sourced yet
            /// (LEFT JOIN on the producer-side query).
            /// </summary>
            public string? Status { get; set; }

            /// <summary>
            /// Human-readable status (e.g. "In Progress", "Final"). For display.
            /// Null when CompetitionStatus hasn't been sourced.
            /// </summary>
            public string? StatusDescription { get; set; }

            public string? Broadcasts { get; set; }

            // Teams
            public string Away { get; set; } = default!;
            public string AwayShort { get; set; } = default!;
            public Guid AwayFranchiseSeasonId { get; set; }
            public string AwayLogoUri { get; set; } = null!;
            public string? AwayLogoUriDark { get; set; }
            public string AwaySlug { get; set; } = default!;
            public string AwayColor { get; set; } = default!;
            public int? AwayRank { get; set; }
            public int AwayWins { get; set; }
            public int AwayLosses { get; set; }
            public int AwayConferenceWins { get; set; }
            public int AwayConferenceLosses { get; set; }

            public string Home { get; set; } = default!;
            public string HomeShort { get; set; } = default!;
            public Guid HomeFranchiseSeasonId { get; set; }
            public string HomeLogoUri { get; set; } = null!;
            public string? HomeLogoUriDark { get; set; }
            public string HomeSlug { get; set; } = default!;
            public string HomeColor { get; set; } = default!;
            public int? HomeRank { get; set; }
            public int HomeWins { get; set; }
            public int HomeLosses { get; set; }
            public int HomeConferenceWins { get; set; }
            public int HomeConferenceLosses { get; set; }

            // Odds
            public string? SpreadCurrentDetails { get; set; }
            public decimal? SpreadCurrent { get; set; }
            public decimal? SpreadOpen { get; set; }
            public decimal? OverUnderCurrent { get; set; }
            public decimal? OverUnderOpen { get; set; }

            /// <summary>
            /// Sportsbook display name (e.g. "ESPN BET", "DraftKings") whose
            /// spread / O/U values populated the odds fields above. Sourced
            /// per-competition by Producer's preferred-then-fallback provider
            /// selection. Null when no qualifying odds row exists. Future
            /// "view all odds" expansion will surface a per-provider list;
            /// this single field is the single-provider header in the
            /// interim.
            /// </summary>
            public string? ProviderName { get; set; }

            // Venue
            public string Venue { get; set; } = default!;
            public string VenueCity { get; set; } = default!;
            public string VenueState { get; set; } = default!;

            public Guid? AiWinnerFranchiseSeasonId { get; set; }
            public bool IsPreviewAvailable { get; set; }
            public bool IsPreviewReviewed { get; set; }

            // Live game state at rest. Mirrors the fields the per-play
            // SignalR events carry, so a client arriving mid-game — or one
            // sitting through a gap in the stream (commercial break,
            // halftime) — renders a complete live card rather than waiting
            // for the next play. SignalR remains the real-time path and
            // overwrites these on arrival.

            /// <summary>Football period, pre-formatted as SignalR sends it ("Q3"). Null for baseball.</summary>
            public string? Period { get; set; }

            /// <summary>Baseball inning number. Null for football.</summary>
            public int? Inning { get; set; }

            public string? Clock { get; set; }

            /// <summary>Team with the ball (football) or batting (baseball), from the last play.</summary>
            public Guid? PossessionFranchiseSeasonId { get; set; }

            public Guid? LastPlayId { get; set; }
            public string? LastPlayDescription { get; set; }

            // Football snap state, from the latest play. Baseball has no
            // per-play count/runner equivalent, so MLB carries only the
            // sport-neutral fields above.
            public int? Down { get; set; }
            public int? Distance { get; set; }

            /// <summary>Absolute field position, 0–100 from the HOME goal line.</summary>
            public int? BallOnYardLine { get; set; }

            // Result
            public bool IsComplete { get; set; }
            public int? AwayScore { get; set; }
            public int? HomeScore { get; set; }
            public Guid? WinnerFranchiseSeasonId { get; set; }
            public Guid? SpreadWinnerFranchiseSeasonId { get; set; }
            public OverUnderPick? OverUnderResult { get; set; }
            public DateTime? CompletedUtc { get; set; }

            // Streaming (live updates) — non-null when a CompetitionStream row
            // exists in an actionable state (Scheduled / AwaitingStart / Active).
            // Drives the "View" call-to-action on matchup cards.
            public DateTime? StreamScheduledTimeUtc { get; set; }

            // MLB only — null on non-MLB matchups.
            public ProbablePitcherDto? HomeProbablePitcher { get; set; }
            public ProbablePitcherDto? AwayProbablePitcher { get; set; }
        }
    }
}