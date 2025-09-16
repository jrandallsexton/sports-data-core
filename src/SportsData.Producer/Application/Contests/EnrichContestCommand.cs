namespace SportsData.Producer.Application.Contests;

// TODO: This needs Provider, Sport, and ContestType to be useful
public record EnrichContestCommand(Guid ContestId, Guid CorrelationId);