namespace SportsData.Api.Application.Admin.Commands.BackfillLeagueScores;

public record BackfillLeagueScoresCommand(int SeasonYear);

public record BackfillLeagueScoresResult(
    int SeasonYear,
    int TotalWeeks,
    int ProcessedWeeks,
    int Errors,
    string Message
)
{
    /// <summary>
    /// Creates a default BackfillLeagueScoresResult with all numeric fields set to 0 and an empty message.
    /// </summary>
    /// <returns>A BackfillLeagueScoresResult where SeasonYear, TotalWeeks, ProcessedWeeks, and Errors are 0 and Message is an empty string.</returns>
    public static BackfillLeagueScoresResult Empty() => new(
        SeasonYear: 0,
        TotalWeeks: 0,
        ProcessedWeeks: 0,
        Errors: 0,
        Message: string.Empty
    );
};