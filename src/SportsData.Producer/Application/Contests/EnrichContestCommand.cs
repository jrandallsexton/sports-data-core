namespace SportsData.Producer.Application.Contests;

public record EnrichContestCommand(Guid ContestId, Guid CorrelationId);