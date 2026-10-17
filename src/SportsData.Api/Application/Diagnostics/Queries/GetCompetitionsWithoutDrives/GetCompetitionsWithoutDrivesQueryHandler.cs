using SportsData.Core.Common;
using SportsData.Core.Dtos.Competition;
using SportsData.Core.Infrastructure.Clients.Contest;

namespace SportsData.Api.Application.Diagnostics.Queries.GetCompetitionsWithoutDrives;

public interface IGetCompetitionsWithoutDrivesQueryHandler
{
    Task<Result<List<CompetitionWithoutDrivesDto>>> ExecuteAsync(
        GetCompetitionsWithoutDrivesQuery query,
        CancellationToken cancellationToken);
}

/// <summary>
/// Aggregates every Producer's competitions-without-drives answer (one sport
/// each, rows stamped with their sport). Producer owns the query; see
/// <see cref="ProducerFanOut"/>.
/// </summary>
public class GetCompetitionsWithoutDrivesQueryHandler : IGetCompetitionsWithoutDrivesQueryHandler
{
    private readonly IContestClientFactory _contestClientFactory;
    private readonly ILogger<GetCompetitionsWithoutDrivesQueryHandler> _logger;

    public GetCompetitionsWithoutDrivesQueryHandler(
        IContestClientFactory contestClientFactory,
        ILogger<GetCompetitionsWithoutDrivesQueryHandler> logger)
    {
        _contestClientFactory = contestClientFactory;
        _logger = logger;
    }

    public Task<Result<List<CompetitionWithoutDrivesDto>>> ExecuteAsync(
        GetCompetitionsWithoutDrivesQuery query,
        CancellationToken cancellationToken)
    {
        return ProducerFanOut.CollectAsync(
            ProducerFanOut.Sports,
            (sport, ct) => _contestClientFactory.Resolve(sport).GetCompetitionsWithoutDrives(query.SeasonYear, ct),
            _logger,
            "competitions without drives",
            cancellationToken);
    }
}
