using System.Text.Json.Serialization;
using SportsData.Core.Converters;

namespace SportsData.Api.Application.Processors;

public class MatchupPreviewResponse
{
    public required string Overview { get; set; }

    public required string Analysis { get; set; }

    public required string Prediction { get; set; }

    public Guid PredictedStraightUpWinner { get; set; }

    public Guid PredictedSpreadWinner { get; set; }

    [JsonConverter(typeof(FlexibleStringConverter))]
    public required string OverUnderPrediction { get; set; }

    public int AwayScore { get; set; }

    public int HomeScore { get; set; }
}