namespace SportsData.Producer.Application.Competitions.Commands.RefreshCompetitionMedia;

public record RefreshCompetitionMediaCommand(
    Guid CompetitionId,
    bool RemoveExisting = false);
