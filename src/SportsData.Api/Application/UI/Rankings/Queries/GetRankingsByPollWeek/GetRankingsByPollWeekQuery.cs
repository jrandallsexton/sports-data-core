namespace SportsData.Api.Application.UI.Rankings.Queries.GetRankingsByPollWeek;

public class GetRankingsByPollWeekQuery
{
    public required int SeasonYear { get; init; }

    public required int Week { get; init; }

    public required string Poll { get; init; }
}
