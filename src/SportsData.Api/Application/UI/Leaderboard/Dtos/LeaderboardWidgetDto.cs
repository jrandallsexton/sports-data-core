namespace SportsData.Api.Application.UI.Leaderboard.Dtos
{
    public class LeaderboardWidgetDto
    {
        public int SeasonYear { get; set; }

        public int AsOfWeek { get; set; }

        public List<WidgetItem> Items { get; set; } = [];

        public class WidgetItem
        {
            public Guid LeagueId { get; set; }

            public required string Name { get; set; }

            public int Rank { get; set; }
        }
    }
}
