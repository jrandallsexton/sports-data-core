namespace SportsData.Api.Application.UI.Leagues.Dtos
{
    public class LeagueScoresByWeekDto
    {
        public Guid LeagueId { get; set; }

        public required string LeagueName { get; set; }

        public List<LeagueScoreByWeek> Weeks { get; set; } = [];

        public class LeagueScoreByWeek
        {
            public int WeekNumber { get; set; }

            public int PickCount { get; set; }

            public List<LeagueUserScoreDto> UserScores { get; set; } = [];
        }

        public class LeagueUserScoreDto
        {
            public Guid UserId { get; set; }

            public required string UserName { get; set; }

            public bool IsSynthetic { get; set; }

            public int WeekNumber { get; set; }

            public int PickCount { get; set; }

            public int Score { get; set; }

            public bool IsDropWeek { get; set; }

            public bool IsWeeklyWinner { get; set; }

            public int? Rank { get; set; }
        }
    }
}
