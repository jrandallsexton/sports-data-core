namespace SportsData.Api.Application.UI.Rankings.Queries.GetRankingsByPollSeasonWeekId;

public class GetRankingsByPollSeasonWeekIdQuery
{
    public required Guid SeasonWeekId { get; init; }

    public required string Poll { get; init; }
}
