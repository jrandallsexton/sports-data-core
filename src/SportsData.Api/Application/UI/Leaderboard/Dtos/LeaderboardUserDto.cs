namespace SportsData.Api.Application.UI.Leaderboard.Dtos
{
    public class LeaderboardUserDto
    {
        public Guid UserId { get; set; }         // Unique user ID

        public string Name { get; set; } = "";   // Display name

        public int TotalPoints { get; set; }     // Across all weeks up to now

        public int CurrentWeekPoints { get; set; }    // For a single week

        public decimal WeeklyAverage { get; set; }

        public int Rank { get; set; }            // Current rank in the leaderboard

        public int? LastWeekRank { get; set; }   // Optional for movement indicator
    }

}
