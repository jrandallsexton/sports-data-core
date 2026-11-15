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
public record GetFranchiseSplitsQuery(
    string Slug,
    int SeasonFrom,
    int SeasonTo,
    GameSite? Site = null,
    OpponentRankFilter? OpponentRank = null,
    LineFilter? Line = null,
    KickoffWindow? KickoffWindow = null,
    bool? ConferenceGame = null);

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
