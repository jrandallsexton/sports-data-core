using System;

namespace SportsData.Core.Dtos.Canonical;

/// <summary>
/// Raw matchup data for league pick'em display.
/// Returned by Producer, mapped to MatchupForPickDto in API.
/// </summary>
public class LeagueMatchupDto
{
    public Guid SeasonWeekId { get; set; }
    public Guid ContestId { get; set; }
    public DateTime StartDateUtc { get; set; }
    public string? Status { get; set; }
    public string? Broadcasts { get; set; }

    public string? Venue { get; set; }
    public string? VenueCity { get; set; }
    public string? VenueState { get; set; }

    public string Away { get; set; } = default!;
    public string AwayShort { get; set; } = default!;
    public Guid AwayFranchiseSeasonId { get; set; }
    public string? AwayLogoUri { get; set; }
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
}
