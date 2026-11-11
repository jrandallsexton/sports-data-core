using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.Analytics.Queries.GetAthleteSeasonLeaders;

public class GetAthleteSeasonLeadersQuery
{
    public required int SeasonYear { get; init; }
    public required Sport Sport { get; init; }
    public required string Category { get; init; }
    public required string Stat { get; init; }
    public bool PerGame { get; init; }
    public int Limit { get; init; } = 10;
}
