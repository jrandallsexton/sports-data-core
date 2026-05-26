using FluentValidation.Results;

using SportsData.Core.Common;
using SportsData.Core.Common.Mapping;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.Clients.Franchise;

namespace SportsData.Api.Application.UI.TeamCard.Queries.GetTeamFinalizedGames;

public interface IGetTeamFinalizedGamesQueryHandler
{
    Task<Result<List<TeamCardScheduleItemDto>>> ExecuteAsync(
        GetTeamFinalizedGamesQuery query,
        CancellationToken cancellationToken = default);
}

public class GetTeamFinalizedGamesQueryHandler : IGetTeamFinalizedGamesQueryHandler
{
    private readonly ILogger<GetTeamFinalizedGamesQueryHandler> _logger;
    private readonly IFranchiseClientFactory _franchiseClientFactory;

    public GetTeamFinalizedGamesQueryHandler(
        ILogger<GetTeamFinalizedGamesQueryHandler> logger,
        IFranchiseClientFactory franchiseClientFactory)
    {
        _logger = logger;
        _franchiseClientFactory = franchiseClientFactory;
    }

    public async Task<Result<List<TeamCardScheduleItemDto>>> ExecuteAsync(
        GetTeamFinalizedGamesQuery query,
        CancellationToken cancellationToken = default)
    {
        Sport mode;
        try
        {
            mode = ModeMapper.ResolveMode(query.Sport, query.League);
        }
        catch (NotSupportedException)
        {
            // Bad route segments — surface as 400 (Validation), not 500. The
            // generic catch below would otherwise mask a client input error as
            // an internal error.
            return new Failure<List<TeamCardScheduleItemDto>>(
                new List<TeamCardScheduleItemDto>(),
                ResultStatus.Validation,
                [
                    new ValidationFailure(nameof(query.Sport), $"Unsupported sport: '{query.Sport}'"),
                    new ValidationFailure(nameof(query.League), $"Unsupported league: '{query.League}'")
                ]);
        }

        try
        {
            var client = _franchiseClientFactory.Resolve(mode);
            return await client.GetTeamFinalizedGames(query.Slug, query.SeasonYear, query.AsOfDate, cancellationToken);
        }
        catch (Exception ex)
        {
            // CWE-117 (log forging): route segments are user-controlled, so strip
            // CR/LF before they reach plain-text log sinks. Structured sinks (Seq)
            // are already safe via JSON property escaping; this is defense in
            // depth for file/stdout sinks.
            _logger.LogError(
                ex,
                "Error retrieving team finalized games for sport={Sport}, league={League}, slug={Slug}, seasonYear={SeasonYear}, asOfDate={AsOfDate}",
                SanitizeForLog(query.Sport), SanitizeForLog(query.League), SanitizeForLog(query.Slug), query.SeasonYear, query.AsOfDate);

            return new Failure<List<TeamCardScheduleItemDto>>(
                new List<TeamCardScheduleItemDto>(),
                ResultStatus.Error,
                [new ValidationFailure("TeamFinalizedGames", "Error retrieving team finalized games. Please try again later.")]);
        }
    }

    private static string SanitizeForLog(string? value) =>
        value is null ? string.Empty : value.Replace("\r", string.Empty).Replace("\n", string.Empty);
}
