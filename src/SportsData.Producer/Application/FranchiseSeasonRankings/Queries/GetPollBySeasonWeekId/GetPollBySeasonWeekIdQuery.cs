namespace SportsData.Producer.Application.FranchiseSeasonRankings.Queries.GetPollBySeasonWeekId;

public class GetPollBySeasonWeekIdQuery
{
    public required Guid SeasonWeekId { get; init; }
    public required string PollSlug { get; init; }
}
