using SportsData.Api.Application.UI.Picks.Advisor.Planner;

namespace SportsData.Api.Application.UI.Picks.Advisor.Queries.GetPickAdvice;

public class GetPickAdviceQuery
{
    public required Guid UserId { get; init; }
    public required Guid LeagueId { get; init; }
    public required int Week { get; init; }

    /// <summary>Null = build the sheet for the level StatBot recommends.</summary>
    public AdvisorLevel? Level { get; init; }
}
