namespace SportsData.Api.Application.Contests.Queries.GetContestHistory;

public record GetContestHistoryQuery(
    string Sport,
    string League,
    Guid ContestId);
