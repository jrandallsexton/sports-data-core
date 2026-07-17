using SportsData.Core.Common;
using SportsData.Core.Common.Mapping;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.Clients.Season;

namespace SportsData.Api.Application.Seasons.Queries.GetCurrentSeason;

public interface IGetCurrentSeasonQueryHandler
{
    Task<Result<CurrentSeasonDto>> ExecuteAsync(
        GetCurrentSeasonQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Per-sport pass-through to Producer's current-or-upcoming season. Maps the
/// route's sport/league to a mode and resolves the sport-specific SeasonClient;
/// returns raw phase data for the caller to interpret (e.g. the off-season
/// kickoff countdown reading the Regular Season phase's StartDate).
/// </summary>
public class GetCurrentSeasonQueryHandler : IGetCurrentSeasonQueryHandler
{
    private readonly ISeasonClientFactory _seasonClientFactory;
    private readonly ILogger<GetCurrentSeasonQueryHandler> _logger;

    public GetCurrentSeasonQueryHandler(
        ISeasonClientFactory seasonClientFactory,
        ILogger<GetCurrentSeasonQueryHandler> logger)
    {
        _seasonClientFactory = seasonClientFactory;
        _logger = logger;
    }

    public async Task<Result<CurrentSeasonDto>> ExecuteAsync(
        GetCurrentSeasonQuery query,
        CancellationToken cancellationToken = default)
    {
        Sport mode;
        try
        {
            mode = ModeMapper.ResolveMode(query.Sport, query.League);
        }
        catch (NotSupportedException ex)
        {
            _logger.LogWarning(ex,
                "Unsupported sport/league combination. Sport={Sport}, League={League}",
                query.Sport.Sanitize(), query.League.Sanitize());
            return new Failure<CurrentSeasonDto>(
                default!,
                ResultStatus.BadRequest,
                [new FluentValidation.Results.ValidationFailure("Sport/League", ex.Message)]);
        }

        var client = _seasonClientFactory.Resolve(mode);
        return await client.GetCurrentSeason(cancellationToken);
    }
}
