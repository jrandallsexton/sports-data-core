namespace SportsData.Api.Application.Admin.Commands.BackfillLeagueScores;

public record BackfillLeagueScoresCommand(int SeasonYear);

public record BackfillLeagueScoresResult(
    int SeasonYear,
    int TotalWeeks,
    int ProcessedWeeks,
    int Errors,
    string Message
);
