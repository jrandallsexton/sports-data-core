using SportsData.Core.Common;
using SportsData.Core.Dtos.Competition;
using SportsData.Core.Infrastructure.Clients.Contest;

namespace SportsData.Api.Application.Diagnostics.Queries.GetCompetitionsWithoutCompetitors;

public interface IGetCompetitionsWithoutCompetitorsQueryHandler
{
    Task<Result<List<CompetitionWithoutCompetitorsDto>>> ExecuteAsync(
        GetCompetitionsWithoutCompetitorsQuery query,
        CancellationToken cancellationToken);
}

/// <summary>
/// Aggregates every Producer's competitions-without-competitors answer (one
/// sport each, rows stamped with their sport). Producer owns the query; see
/// <see cref="ProducerFanOut"/>.
/// </summary>
public class GetCompetitionsWithoutCompetitorsQueryHandler : IGetCompetitionsWithoutCompetitorsQueryHandler
{
    private readonly IContestClientFactory _contestClientFactory;
    private readonly ILogger<GetCompetitionsWithoutCompetitorsQueryHandler> _logger;

    public GetCompetitionsWithoutCompetitorsQueryHandler(
        IContestClientFactory contestClientFactory,
        ILogger<GetCompetitionsWithoutCompetitorsQueryHandler> logger)
    {
        _contestClientFactory = contestClientFactory;
        _logger = logger;
    }

    public Task<Result<List<CompetitionWithoutCompetitorsDto>>> ExecuteAsync(
        GetCompetitionsWithoutCompetitorsQuery query,
        CancellationToken cancellationToken)
    {
        return ProducerFanOut.CollectAsync(
            ProducerFanOut.Sports,
            (sport, ct) => _contestClientFactory.Resolve(sport).GetCompetitionsWithoutCompetitors(ct),
            row => row.Sport,
            _logger,
            "competitions without competitors",
            cancellationToken);
    }
}
