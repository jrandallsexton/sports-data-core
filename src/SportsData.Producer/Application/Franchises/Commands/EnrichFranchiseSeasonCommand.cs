namespace SportsData.Producer.Application.Franchises.Commands;

public record EnrichFranchiseSeasonCommand(
    Guid FranchiseSeasonId,
    int SeasonYear,
    Guid CorrelationId);