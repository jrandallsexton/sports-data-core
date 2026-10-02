namespace SportsData.Producer.Application.Contests.Jobs.ContestEnrichment;

public record EnrichContestCommand(Guid ContestId, Guid CorrelationId);