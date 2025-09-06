namespace SportsData.Api.Application.Previews.Models;

public class MatchupPreviewResponse
{
    public required string Overview { get; set; }

    public required string Analysis { get; set; }

    public required string Prediction { get; set; }

    public Guid PredictedStraightUpWinner { get; set; }

    public Guid? PredictedSpreadWinner { get; set; }

    public int? OverUnderPrediction { get; set; }

    public int AwayScore { get; set; }

    public int HomeScore { get; set; }
}