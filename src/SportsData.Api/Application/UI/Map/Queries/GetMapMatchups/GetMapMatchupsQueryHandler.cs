using SportsData.Api.Application.UI.Map.Dtos;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.Map.Queries.GetMapMatchups;

public interface IGetMapMatchupsQueryHandler
{
    Task<Result<GetMapMatchupsResponse>> ExecuteAsync(
        GetMapMatchupsQuery query,
        CancellationToken cancellationToken = default);
}

public class GetMapMatchupsQueryHandler : IGetMapMatchupsQueryHandler
{
    private readonly ILogger<GetMapMatchupsQueryHandler> _logger;
    private readonly IProvideCanonicalData _canonicalDataProvider;

    public GetMapMatchupsQueryHandler(
        ILogger<GetMapMatchupsQueryHandler> logger,
        IProvideCanonicalData canonicalDataProvider)
    {
        _logger = logger;
        _canonicalDataProvider = canonicalDataProvider;
    }

    public async Task<Result<GetMapMatchupsResponse>> ExecuteAsync(
        GetMapMatchupsQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Getting map matchups. LeagueId={LeagueId}, WeekNumber={WeekNumber}",
            query.LeagueId,
            query.WeekNumber);

        if (query.LeagueId is null && query.WeekNumber is null)
        {
            // TODO: Honor WeekNumber when LeagueId is provided
            var matchups = await _canonicalDataProvider.GetMatchupsForCurrentWeek();

            return new Success<GetMapMatchupsResponse>(new GetMapMatchupsResponse
            {
                Matchups = matchups
            });
        }
        else if (query.LeagueId is null)
        {
            // we have a week, but no league
            var seasonYear = query.SeasonYear ?? DateTime.UtcNow.Year;
            var matchups = await _canonicalDataProvider.GetMatchupsForSeasonWeek(seasonYear, query.WeekNumber!.Value);

            return new Success<GetMapMatchupsResponse>(new GetMapMatchupsResponse
            {
                Matchups = matchups
            });
        }
        else
        {
            // we have a league (and optionally a week)
            var matchups = await _canonicalDataProvider.GetMatchupsForCurrentWeek();

            return new Success<GetMapMatchupsResponse>(new GetMapMatchupsResponse
            {
                Matchups = matchups
            });
        }
    }
}
