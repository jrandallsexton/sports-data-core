namespace SportsData.Api.Application.Contests.Queries.GetContestById;

public record GetContestByIdQuery(
    string Sport,
    string League,
    Guid ContestId);
