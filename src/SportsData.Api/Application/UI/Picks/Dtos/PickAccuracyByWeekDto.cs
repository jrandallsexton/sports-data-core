namespace SportsData.Api.Application.UI.Picks.Dtos
{
    public class PickAccuracyByWeekDto
    {
        public Guid UserId { get; set; }

        public string UserName { get; set; } = default!;

        public Guid LeagueId { get; set; }

        public string LeagueName { get; set; } = default!;

        public List<WeeklyAccuracyDto> WeeklyAccuracy { get; set; } = new();

        public double OverallAccuracyPercent { get; set; }

        public class WeeklyAccuracyDto
        {
            public int Week { get; set; }
            public int CorrectPicks { get; set; }
            public int TotalPicks { get; set; }

            public double AccuracyPercent { get; set; }
        }
    }
}
