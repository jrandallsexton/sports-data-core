namespace SportsData.Producer.Application.Competitions.Commands.EnqueueCompetitionMediaRefresh;

public record EnqueueCompetitionMediaRefreshCommand(Guid CompetitionId, bool RemoveExisting = true);
