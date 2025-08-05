namespace SportsData.Api.Application.UI.TeamCard.Queries
{
    public class GetTeamCardQuery
    {
        public required string Sport { get; init; }

        public required string League { get; init; }

        public required string Slug { get; init; }

        public required int SeasonYear { get; init; }
    }

}
