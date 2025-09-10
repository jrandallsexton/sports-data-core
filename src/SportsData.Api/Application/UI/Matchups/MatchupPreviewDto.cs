namespace SportsData.Api.Application.UI.Matchups
{
    public class MatchupPreviewDto
    {
        public Guid Id { get; set; }

        public Guid ContestId { get; set; }

        public string? Overview { get; set; }

        public string? Analysis { get; set; }

        public string? Prediction { get; set; }

        public string? StraightUpWinner { get; set; }

        public string? AtsWinner { get; set; }

        public int? AwayScore { get; set; }

        public int? HomeScore { get; set; }

        public string? VegasImpliedScore { get; set; }
    }
}
