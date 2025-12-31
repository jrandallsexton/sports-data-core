namespace SportsData.Api.Application.UI.Leagues.Commands.GenerateLeagueWeekPreviews;

public class GenerateLeagueWeekPreviewsCommand
{
    public required Guid LeagueId { get; init; }

    public required int WeekNumber { get; init; }
}
