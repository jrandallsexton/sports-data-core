namespace SportsData.Api.Application.UI.Picks.Queries.GetUserPicksByGroupAndWeek;

public class GetUserPicksByGroupAndWeekQuery
{
    public required Guid UserId { get; init; }

    public required Guid GroupId { get; init; }

    public required int WeekNumber { get; init; }
}
