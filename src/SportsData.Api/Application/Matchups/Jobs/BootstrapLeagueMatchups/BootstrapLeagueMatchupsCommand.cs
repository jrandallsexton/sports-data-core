namespace SportsData.Api.Application.Matchups.Jobs.BootstrapLeagueMatchups;

public record BootstrapLeagueMatchupsCommand(
    Guid GroupId,
    Guid CorrelationId);