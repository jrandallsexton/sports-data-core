using System;

namespace SportsData.Core.Dtos.Canonical;

/// <summary>
/// Raw matchup data for league pick'em display.
/// Returned by Producer, mapped to MatchupForPickDto in API.
/// </summary>
public class LeagueMatchupDto
{
    public Guid SeasonWeekId { get; set; }

    /// <summary>
    /// EndDate of the SeasonWeek this contest belongs to. Used by the API
    /// matchups handler to compute LeagueWeekMatchupsDto.AsOfDate — the
    /// inclusive boundary the mini-schedule endpoint uses to filter completed
    /// games. All contests in a single league-week share the same SeasonWeek,
    /// so any row's value is authoritative.
    /// </summary>
    public DateTime SeasonWeekEndDate { get; set; }

    public Guid ContestId { get; set; }
    public DateTime StartDateUtc { get; set; }
    /// <summary>
    /// Raw ESPN status type name (e.g. "STATUS_IN_PROGRESS", "STATUS_FINAL")
    /// for programmatic branching. Pair with <see cref="StatusDescription"/>
    /// for display.
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// Human-readable status (e.g. "In Progress", "Final"). For display.
    /// </summary>
    public string? StatusDescription { get; set; }

    public string? Broadcasts { get; set; }

    /// <summary>
    /// Marquee headline pulled live from CompetitionNote.Headline (Type=event).
    /// Set for special games (bowl names, conference championships, postseason
    /// designations). Null otherwise.
    /// </summary>
    public string? Headline { get; set; }

    /// <summary>
    /// Baseball-only series state snapshot (e.g. "BOS leads series 2-0"), pulled
    /// from BaseballCompetition.CurrentSeriesSummary. Null on non-baseball
    /// matchups and on baseball games not part of a current series.
    /// </summary>
    public string? CurrentSeriesSummary { get; set; }

    public string? Venue { get; set; }
    public string? VenueCity { get; set; }
    public string? VenueState { get; set; }

    public string Away { get; set; } = default!;
    public string AwayShort { get; set; } = default!;
    public Guid AwayFranchiseSeasonId { get; set; }
    public string? AwayLogoUri { get; set; }
    public string? AwayLogoUriDark { get; set; }
    public string AwaySlug { get; set; } = default!;
    public string? AwayColor { get; set; }
    public int? AwayRank { get; set; }
    public string? AwayConferenceSlug { get; set; }
    public int AwayWins { get; set; }
    public int AwayLosses { get; set; }
    public int AwayConferenceWins { get; set; }
    public int AwayConferenceLosses { get; set; }

    public string Home { get; set; } = default!;
    public string HomeShort { get; set; } = default!;
    public Guid HomeFranchiseSeasonId { get; set; }
    public string? HomeLogoUri { get; set; }
    public string? HomeLogoUriDark { get; set; }
    public string HomeSlug { get; set; } = default!;
    public string? HomeColor { get; set; }
    public int? HomeRank { get; set; }
    public string? HomeConferenceSlug { get; set; }
    public int HomeWins { get; set; }
    public int HomeLosses { get; set; }
    public int HomeConferenceWins { get; set; }
    public int HomeConferenceLosses { get; set; }

    public string? SpreadCurrentDetails { get; set; }
    public double? SpreadCurrent { get; set; }
    public double? SpreadOpen { get; set; }
    public double? OverUnderCurrent { get; set; }
    public double? OverUnderOpen { get; set; }
    public double? OverOdds { get; set; }
    public double? UnderOdds { get; set; }

    /// <summary>
    /// Display name of the sportsbook whose spread / O/U values populated
    /// the odds fields above. Selected by Producer's SQL via the
    /// preferred-then-fallback provider ordering on <c>CompetitionOdds</c>.
    /// Null when no qualifying odds row exists for this competition.
    /// Future per-league preferred-provider work will replace the global
    /// preference baked into the SQL today.
    /// </summary>
    public string? ProviderName { get; set; }

    public int? AwayScore { get; set; }
    public int? HomeScore { get; set; }
    public Guid? WinnerFranchiseSeasonId { get; set; }
    public Guid? SpreadWinnerFranchiseSeasonId { get; set; }
    public int? OverUnderResult { get; set; }
    public DateTime? CompletedUtc { get; set; }

    /// <summary>
    /// Scheduled fire time of the live-streaming Hangfire job for this contest's
    /// competition, when one exists in an actionable state (Scheduled,
    /// AwaitingStart, Active). Null otherwise. Drives a "View" call-to-action in
    /// the UI; future enhancements can render a countdown.
    /// </summary>
    public DateTime? StreamScheduledTimeUtc { get; set; }

    // MLB only — null on non-MLB matchups. Stitched onto the SQL result
    // by GetMatchupsByContestIdsQueryHandler from the Probables ingestion
    // landed in PR #302.
    public ProbablePitcherDto? HomeProbablePitcher { get; set; }
    public ProbablePitcherDto? AwayProbablePitcher { get; set; }
}
