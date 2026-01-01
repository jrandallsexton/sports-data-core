namespace SportsData.Api.Application.UI.Rankings.Queries.GetPollRankingsByWeek;

public class GetPollRankingsByWeekQuery
{
    public required int SeasonYear { get; init; }

    public required int Week { get; init; }
}
