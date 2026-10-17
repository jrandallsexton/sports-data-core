using SportsData.Core.Common;
using SportsData.Core.Dtos.Competition;
using SportsData.Core.Infrastructure.Clients.Contest;

namespace SportsData.Api.Application.Diagnostics.Queries.GetCompetitionsWithoutMetrics;

public interface IGetCompetitionsWithoutMetricsQueryHandler
{
    Task<Result<List<CompetitionWithoutMetricsDto>>> ExecuteAsync(
        GetCompetitionsWithoutMetricsQuery query,
        CancellationToken cancellationToken);
}

/// <summary>
/// Aggregates every Producer's competitions-without-metrics answer (one sport
/// each, rows stamped with their sport). Producer owns the query; see
/// <see cref="ProducerFanOut"/>.
/// </summary>
public class GetCompetitionsWithoutMetricsQueryHandler : IGetCompetitionsWithoutMetricsQueryHandler
{
    private readonly IContestClientFactory _contestClientFactory;
    private readonly ILogger<GetCompetitionsWithoutMetricsQueryHandler> _logger;

    public GetCompetitionsWithoutMetricsQueryHandler(
        IContestClientFactory contestClientFactory,
        ILogger<GetCompetitionsWithoutMetricsQueryHandler> logger)
    {
        _contestClientFactory = contestClientFactory;
        _logger = logger;
    }

    public Task<Result<List<CompetitionWithoutMetricsDto>>> ExecuteAsync(
        GetCompetitionsWithoutMetricsQuery query,
        CancellationToken cancellationToken)
    {
        return ProducerFanOut.CollectAsync(
            ProducerFanOut.Sports,
            (sport, ct) => _contestClientFactory.Resolve(sport).GetCompetitionsWithoutMetrics(query.SeasonYear, ct),
            _logger,
            "competitions without metrics",
            cancellationToken);
    }
}
