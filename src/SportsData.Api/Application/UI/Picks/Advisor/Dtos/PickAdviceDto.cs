using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Application.UI.Picks.Advisor.Planner;

namespace SportsData.Api.Application.UI.Picks.Advisor.Dtos;

/// <summary>
/// StatBot's advice for one league-week: the standings analysis, the level
/// StatBot recommends, the level actually applied, and a full suggested
/// sheet. Nothing here is written; the client applies through the normal
/// submit path. See docs/features/statbot-advisor.md.
/// </summary>
public sealed record PickAdviceDto
{
    public Guid LeagueId { get; init; }
    public int Week { get; init; }
    public PickType PickType { get; init; }
    public bool UseConfidencePoints { get; init; }

    public AdvisorLevel RecommendedLevel { get; init; }

    /// <summary>The level the sheet was built for: the request's, or the recommendation when none was given.</summary>
    public AdvisorLevel Level { get; init; }

    public PickAdviceAnalysisDto Analysis { get; init; } = default!;

    public List<AdvisedPickDto> Picks { get; init; } = [];

    public int FlipCount { get; init; }
    public int CoinFlipCount { get; init; }
    public int LockedCount { get; init; }
    public int NoPredictionCount { get; init; }
}

/// <summary>
/// Standings and performance only. Rule zero: no field here derives from
/// any other member's picks.
/// </summary>
public sealed record PickAdviceAnalysisDto
{
    /// <summary>Null until the user has a scored week in this league.</summary>
    public int? Rank { get; init; }
    public int? LastWeekRank { get; init; }
    public int MemberCount { get; init; }

    public int TotalPoints { get; init; }
    public decimal WeeklyAverage { get; init; }

    /// <summary>Points over decided picks — the comparable rate, since slate size varies week to week.</summary>
    public decimal PointsPerGame { get; init; }
    public decimal PickAccuracy { get; init; }

    public string? LeaderName { get; init; }
    public int LeaderTotalPoints { get; init; }
    public decimal LeaderWeeklyAverage { get; init; }
    public decimal LeaderPointsPerGame { get; init; }

    /// <summary>Leader total minus the user's; zero or negative when leading.</summary>
    public int Deficit { get; init; }

    /// <summary>
    /// Regular-season weeks on the season calendar still ahead (end date
    /// after now). From the Season service, not the league's own week rows.
    /// Null when the calendar could not be read; the copy omits it then.
    /// </summary>
    public int? RegularSeasonWeeksLeft { get; init; }

    /// <summary>Games in the requested week not yet locked. The only game count that is a fact; future slates are never forecast.</summary>
    public int GamesThisWeek { get; init; }

    /// <summary>
    /// The most points this week's open games can yield the caller: the
    /// confidence values 1..N over the slate minus whatever locked picks
    /// already hold, or simply the open-game count in a non-confidence league.
    /// </summary>
    public int MaxPointsThisWeek { get; init; }

    /// <summary>What the leader scores this week at their usual pace: points per game × this week's games.</summary>
    public int LeaderExpectedThisWeek { get; init; }

    /// <summary>
    /// True when a perfect week beats the leader even if the leader scores
    /// at pace. False when leading (nothing to close) or when it can't be
    /// done. No per-game target is ever offered.
    /// </summary>
    public bool CanCloseGapThisWeek { get; init; }

    /// <summary>
    /// The best rank a perfect week could produce with everyone above
    /// scoring at their usual pace: one plus the members whose total plus
    /// expected week still meets or beats the caller's total plus this
    /// week's maximum. Standings arithmetic only. Null until ranked.
    /// </summary>
    public int? BestCaseRankThisWeek { get; init; }

    /// <summary>The member directly ahead in the standings, if any.</summary>
    public string? NextAheadName { get; init; }
    public int? NextAheadRank { get; init; }
    public int? PointsBehindNextAhead { get; init; }

    /// <summary>What the member directly ahead scores this week at their usual pace.</summary>
    public int? NextAheadExpectedThisWeek { get; init; }

    /// <summary>Spread of points-per-game across scored member-weeks; null when fewer than two exist.</summary>
    public double? PointsPerGameStdDev { get; init; }

    /// <summary>StatBot's own standing in this league; null when StatBot is not a scored member.</summary>
    public PickAdviceStatBotDto? StatBot { get; init; }
}

public sealed record PickAdviceStatBotDto
{
    public int Rank { get; init; }
    public int TotalPoints { get; init; }
    public decimal WeeklyAverage { get; init; }
    public decimal PointsPerGame { get; init; }
}

public sealed record AdvisedPickDto
{
    public Guid ContestId { get; init; }
    public string? Headline { get; init; }
    public AdvisedPickKind Kind { get; init; }
    public Guid? FranchiseSeasonId { get; init; }
    public int? ConfidencePoints { get; init; }
    public double? ModelProbability { get; init; }
    public bool? PreviewAgrees { get; init; }
    public bool IsCoinFlip { get; init; }
    public bool DiffersFromExisting { get; init; }
}
