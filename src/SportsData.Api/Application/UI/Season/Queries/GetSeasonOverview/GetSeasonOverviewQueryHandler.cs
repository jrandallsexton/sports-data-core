using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.Clients.Season;

namespace SportsData.Api.Application.UI.Season.Queries.GetSeasonOverview;

public interface IGetSeasonOverviewQueryHandler
{
    Task<Result<SeasonOverviewDto>> ExecuteAsync(
        GetSeasonOverviewQuery query,
        CancellationToken cancellationToken = default);
}

public class GetSeasonOverviewQueryHandler : IGetSeasonOverviewQueryHandler
{
    private readonly ILogger<GetSeasonOverviewQueryHandler> _logger;
    private readonly ISeasonClientFactory _seasonClientFactory;

    public GetSeasonOverviewQueryHandler(
        ILogger<GetSeasonOverviewQueryHandler> logger,
        ISeasonClientFactory seasonClientFactory)
    {
        _logger = logger;
        _seasonClientFactory = seasonClientFactory;
    }

    public async Task<Result<SeasonOverviewDto>> ExecuteAsync(
        GetSeasonOverviewQuery query,
        CancellationToken cancellationToken = default)
    {
        var seasonClient = _seasonClientFactory.Resolve(query.Sport);

        var correlationId = ActivityExtensions.GetCorrelationId();

        _logger.LogInformation(
            "GetSeasonOverview requested. SeasonYear={SeasonYear}, CorrelationId={CorrelationId}",
            query.SeasonYear,
            correlationId);

        var result = await seasonClient.GetSeasonOverview(query.SeasonYear, cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogWarning(
                "Failed to get season overview. SeasonYear={SeasonYear}, CorrelationId={CorrelationId}",
                query.SeasonYear,
                correlationId);
        }

        return result;
    }
}
