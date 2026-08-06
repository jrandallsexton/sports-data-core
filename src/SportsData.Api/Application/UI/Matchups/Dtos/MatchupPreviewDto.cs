namespace SportsData.Api.Application.UI.Matchups.Dtos;

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

    public DateTime GeneratedUtc { get; set; }

    /// <summary>
    /// True when the contest this preview describes has finished
    /// (canonical status STATUS_FINAL). The admin approve/reject
    /// affordances are pointless after the game is played — clients hide
    /// them when this is set.
    /// </summary>
    public bool IsContestCompleted { get; set; }
}
