namespace SportsData.Api.Application.UI.Picks.Queries
{
    public class GetUserPicksQuery
    {
        public Guid UserId { get; set; }

        public Guid GroupId { get; set; }

        public int WeekNumber { get; set; }
    }
}
