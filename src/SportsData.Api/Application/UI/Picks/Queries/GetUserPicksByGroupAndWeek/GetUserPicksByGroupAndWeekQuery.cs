namespace SportsData.Api.Application.UI.Picks.Queries.GetUserPicksByGroupAndWeek;

public class GetUserPicksByGroupAndWeekQuery
{
    public Guid UserId { get; set; }

    public Guid GroupId { get; set; }

    public int WeekNumber { get; set; }
}
