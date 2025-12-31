using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;

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
    private readonly IProvideCanonicalData _canonicalDataProvider;

    public GetFranchiseSeasonMetricsQueryHandler(
        ILogger<GetFranchiseSeasonMetricsQueryHandler> logger,
        IProvideCanonicalData canonicalDataProvider)
    {
        _logger = logger;
        _canonicalDataProvider = canonicalDataProvider;
    }

    public async Task<Result<List<FranchiseSeasonMetricsDto>>> ExecuteAsync(
        GetFranchiseSeasonMetricsQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting franchise season metrics for season year {SeasonYear}", query.SeasonYear);

        var metrics = await _canonicalDataProvider.GetFranchiseSeasonMetricsBySeasonYear(query.SeasonYear);

        _logger.LogInformation(
            "Found {Count} franchise season metrics for season year {SeasonYear}",
            metrics.Count,
            query.SeasonYear);

        return new Success<List<FranchiseSeasonMetricsDto>>(metrics);
    }
}
