using SportsData.Core.Common;
using SportsData.Core.Dtos.Competition;
using SportsData.Core.Infrastructure.Clients.Contest;

namespace SportsData.Api.Application.Diagnostics.Queries.GetCompetitionsWithoutPlays;

public interface IGetCompetitionsWithoutPlaysQueryHandler
{
    Task<Result<List<CompetitionWithoutPlaysDto>>> ExecuteAsync(
        GetCompetitionsWithoutPlaysQuery query,
        CancellationToken cancellationToken);
}

/// <summary>
/// Aggregates every Producer's competitions-without-plays answer (one sport
/// each, rows stamped with their sport). Producer owns the query; see
/// <see cref="ProducerFanOut"/>.
/// </summary>
public class GetCompetitionsWithoutPlaysQueryHandler : IGetCompetitionsWithoutPlaysQueryHandler
{
    private readonly IContestClientFactory _contestClientFactory;
    private readonly ILogger<GetCompetitionsWithoutPlaysQueryHandler> _logger;

    public GetCompetitionsWithoutPlaysQueryHandler(
        IContestClientFactory contestClientFactory,
        ILogger<GetCompetitionsWithoutPlaysQueryHandler> logger)
    {
        _contestClientFactory = contestClientFactory;
        _logger = logger;
    }

    public Task<Result<List<CompetitionWithoutPlaysDto>>> ExecuteAsync(
        GetCompetitionsWithoutPlaysQuery query,
        CancellationToken cancellationToken)
    {
        return ProducerFanOut.CollectAsync(
            ProducerFanOut.Sports,
            (sport, ct) => _contestClientFactory.Resolve(sport).GetCompetitionsWithoutPlays(query.SeasonYear, ct),
            _logger,
            "competitions without plays",
            cancellationToken);
    }
}
