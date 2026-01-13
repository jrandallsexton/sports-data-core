using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.Clients.Franchise;

namespace SportsData.Api.Application.UI.Analytics.Queries.GetFranchiseSeasonMetrics;

public interface IGetFranchiseSeasonMetricsQueryHandler
{
    Task<Result<List<FranchiseSeasonMetricsDto>>> ExecuteAsync(
        GetFranchiseSeasonMetricsQuery query,
        CancellationToken cancellationToken = default);
}

public class GetFranchiseSeasonMetricsQueryHandler : IGetFranchiseSeasonMetricsQueryHandler
{
    private readonly ILogger<GetFranchiseSeasonMetricsQueryHandler> _logger;
    private readonly IFranchiseClientFactory _franchiseClientFactory;

    public GetFranchiseSeasonMetricsQueryHandler(
        ILogger<GetFranchiseSeasonMetricsQueryHandler> logger,
        IFranchiseClientFactory franchiseClientFactory)
    {
        _logger = logger;
        _franchiseClientFactory = franchiseClientFactory;
    }

    public async Task<Result<List<FranchiseSeasonMetricsDto>>> ExecuteAsync(
        GetFranchiseSeasonMetricsQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting franchise season metrics for season year {SeasonYear}, Sport={Sport}", 
            query.SeasonYear, query.Sport);

        var client = _franchiseClientFactory.Resolve(query.Sport);
        var metrics = await client.GetFranchiseSeasonMetrics(query.SeasonYear, cancellationToken);

        _logger.LogInformation(
            "Found {Count} franchise season metrics for season year {SeasonYear}",
            metrics.Count,
            query.SeasonYear);

        return new Success<List<FranchiseSeasonMetricsDto>>(metrics);
    }
}
