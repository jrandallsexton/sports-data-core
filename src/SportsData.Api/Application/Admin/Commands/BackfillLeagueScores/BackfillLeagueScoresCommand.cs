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
    public static BackfillLeagueScoresResult Empty() => new(
        SeasonYear: 0,
        TotalWeeks: 0,
        ProcessedWeeks: 0,
        Errors: 0,
        Message: string.Empty
    );
};
