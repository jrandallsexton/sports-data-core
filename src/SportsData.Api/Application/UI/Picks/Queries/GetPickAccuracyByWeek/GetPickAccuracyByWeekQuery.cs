namespace SportsData.Api.Application.UI.Picks.Queries.GetPickAccuracyByWeek;

public class GetPickAccuracyByWeekQuery
{
    public Guid UserId { get; set; }

    public bool ForSynthetic { get; set; }
}
