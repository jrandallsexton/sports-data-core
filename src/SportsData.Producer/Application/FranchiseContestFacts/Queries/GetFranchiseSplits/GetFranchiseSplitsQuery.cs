using SportsData.Producer.Enums;

namespace SportsData.Producer.Application.FranchiseContestFacts.Queries.GetFranchiseSplits;

/// <summary>
/// A team's games over a season range, narrowed by optional filters. Each
/// null filter means "any".
/// </summary>
/// <param name="Slug">The franchise slug.</param>
/// <param name="SeasonFrom">First season (inclusive).</param>
/// <param name="SeasonTo">Last season (inclusive).</param>
/// <param name="Site">Home, Away or Neutral.</param>
/// <param name="OpponentRank">Opponent's rank going in.</param>
/// <param name="Line">Favorite or underdog by this team's line.</param>
/// <param name="KickoffWindow">Local kickoff window.</param>
/// <param name="ConferenceGame">Conference (true) or non-conference (false) games.</param>
/// <param name="OpponentConference">The opponent's conference abbreviation in that season (e.g. "sec"), as listed in the response's options.</param>
/// <param name="Opponent">Head-to-head: the opponent's franchise slug. An unknown slug matches no games.</param>
/// <param name="SeasonType">Regular season or postseason.</param>
/// <param name="Month">Local kickoff month, 1-12.</param>
/// <param name="SpreadSize">Size of the line, either side.</param>
/// <param name="Ats">Against-the-spread outcome from this team's side. A game without a line has no outcome and matches none of these.</param>
public record GetFranchiseSplitsQuery(
    string Slug,
    int SeasonFrom,
    int SeasonTo,
    GameSite? Site = null,
    OpponentRankFilter? OpponentRank = null,
    LineFilter? Line = null,
    KickoffWindow? KickoffWindow = null,
    bool? ConferenceGame = null,
    string? OpponentConference = null,
    string? Opponent = null,
    SeasonTypeFilter? SeasonType = null,
    int? Month = null,
    SpreadSizeFilter? SpreadSize = null,
    AtsResult? Ats = null);

/// <summary>Opponent's rank going in. A null (unknown) rank matches none of these.</summary>
public enum OpponentRankFilter
{
    /// <summary>Ranked 1-25.</summary>
    Ranked = 1,

    /// <summary>Ranked 1-10.</summary>
    Top10 = 2,

    /// <summary>Not ranked (ESPN's 99).</summary>
    Unranked = 3
}

/// <summary>This team's side of the line. Pick'em and no line match neither.</summary>
public enum LineFilter
{
    Favorite = 1,
    Underdog = 2
}

/// <summary>
/// ESPN season phase. Conference championship games are regular season in
/// ESPN's data; bowls and the playoff are postseason. All-star phases match
/// neither.
/// </summary>
public enum SeasonTypeFilter
{
    /// <summary>SeasonPhaseTypeCode 2.</summary>
    Regular = 1,

    /// <summary>SeasonPhaseTypeCode 3.</summary>
    Postseason = 2
}

/// <summary>
/// Absolute size of this team's line; direction comes from <see cref="LineFilter"/>.
/// No line matches none of these.
/// </summary>
public enum SpreadSizeFilter
{
    /// <summary>3 or less, including pick'em.</summary>
    Close = 1,

    /// <summary>More than 3, less than 7.</summary>
    Moderate = 2,

    /// <summary>7 to less than 14.</summary>
    Large = 3,

    /// <summary>14 or more.</summary>
    Huge = 4
}
