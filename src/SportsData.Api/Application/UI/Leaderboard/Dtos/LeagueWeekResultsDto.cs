namespace SportsData.Api.Application.UI.Leaderboard.Dtos
{
    public class LeagueWeekResultsDto
    {
        public List<MatchupDto> Matchups { get; set; } = [];

        public List<LeaderboardUserDto> Users { get; set; } = [];

        public class MatchupDto
        {
            public Guid ContestId { get; set; }

            public required string Away { get; set; }

            public required string Home { get; set; }

            public double? Spread { get; set; }

            public bool IsComplete { get; set; }

            public bool? WasHomeWinner { get; set; }

            public bool? WasHomeSpreadWinner { get; set; }
        }
    }
}
