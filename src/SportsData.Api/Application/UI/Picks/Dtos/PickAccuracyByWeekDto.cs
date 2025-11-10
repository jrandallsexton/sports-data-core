namespace SportsData.Api.Application.UI.Picks.Dtos
{
    public record PickAccuracyByWeekDto
    {
        public Guid UserId { get; init; }

        public string UserName { get; init; } = default!;

        public Guid LeagueId { get; init; }

        public string LeagueName { get; init; } = default!;

        public List<WeeklyAccuracyDto> WeeklyAccuracy { get; init; } = [];

        public double OverallAccuracyPercent { get; init; }

        public record WeeklyAccuracyDto
        {
            public int Week { get; init; }
            public int CorrectPicks { get; init; }
            public int TotalPicks { get; init; }

            public double AccuracyPercent { get; init; }
        }
    }
}
