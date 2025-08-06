namespace SportsData.Api.Application.UI.Leagues.Dtos
{
    public class LeagueSummaryDto
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = null!;

        public string Sport { get; set; } = null!;

        public string LeagueType { get; set; } = null!;

        public string? AvatarUrl { get; set; } // optional for visuals
    }
}
