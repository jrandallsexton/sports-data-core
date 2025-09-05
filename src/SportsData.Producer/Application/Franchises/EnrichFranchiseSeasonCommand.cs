namespace SportsData.Producer.Application.Franchises;

public record EnrichFranchiseSeasonCommand(
    Guid FranchiseSeasonId,
    int SeasonYear,
    Guid CorrelationId);