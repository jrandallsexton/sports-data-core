namespace SportsData.Core.Infrastructure.Clients.Franchise.Queries;

/// <summary>
/// Filters for a War Room Splits request. Null means "any". The enum-valued
/// filters are names the Producer binds to its own enums (Site: Home / Away /
/// Neutral; OpponentRank: Ranked / Top10 / Unranked; Line: Favorite /
/// Underdog; KickoffWindow: Noon / Afternoon / Night), so the Producer is
/// the one place that validates them.
/// </summary>
public class FranchiseSplitsRequest
{
    public required int SeasonFrom { get; init; }
    public required int SeasonTo { get; init; }
    public string? Site { get; init; }
    public string? OpponentRank { get; init; }
    public string? Line { get; init; }
    public string? KickoffWindow { get; init; }
    public bool? ConferenceGame { get; init; }

    /// <summary>Opponent's conference abbreviation in that season (a value from the response's options).</summary>
    public string? OpponentConference { get; init; }

    /// <summary>Head-to-head: the opponent's franchise slug.</summary>
    public string? Opponent { get; init; }

    /// <summary>Regular or Postseason.</summary>
    public string? SeasonType { get; init; }

    /// <summary>Local kickoff month, 1-12.</summary>
    public int? Month { get; init; }

    /// <summary>Close, Moderate, Large or Huge.</summary>
    public string? SpreadSize { get; init; }
}
