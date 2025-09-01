namespace SportsData.Producer.Application.Franchises;

public record EnrichFranchiseSeasonCommand(Guid FranchiseSeasonId, Guid CorrelationId);