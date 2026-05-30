using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.Clients.Contest;

namespace SportsData.Api.Application.UI.Contest.Queries.GetContestPlayLog;

public interface IGetContestPlayLogQueryHandler
{
    Task<Result<PlayLogDto>> ExecuteAsync(
        GetContestPlayLogQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// API-side proxy for the Producer's full-play-log endpoint. Mirrors
/// <see cref="GetContestOverview.GetContestOverviewQueryHandler"/> in shape —
/// resolves the sport-keyed contest client, calls the matching method, logs
/// failure for diagnostics. The Producer side does the actual DB work.
/// </summary>
public class GetContestPlayLogQueryHandler : IGetContestPlayLogQueryHandler
{
    private readonly ILogger<GetContestPlayLogQueryHandler> _logger;
    private readonly IContestClientFactory _contestClientFactory;

    public GetContestPlayLogQueryHandler(
        ILogger<GetContestPlayLogQueryHandler> logger,
        IContestClientFactory contestClientFactory)
    {
        _logger = logger;
        _contestClientFactory = contestClientFactory;
    }

    public async Task<Result<PlayLogDto>> ExecuteAsync(
        GetContestPlayLogQuery query,
        CancellationToken cancellationToken = default)
    {
        var contestClient = _contestClientFactory.Resolve(query.Sport);

        var correlationId = ActivityExtensions.GetCorrelationId();

        _logger.LogInformation(
            "GetContestPlayLog requested. ContestId={ContestId}, CorrelationId={CorrelationId}",
            query.ContestId,
            correlationId);

        var result = await contestClient.GetContestPlayLogByContestId(query.ContestId, cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogWarning(
                "Failed to get contest play log. ContestId={ContestId}, CorrelationId={CorrelationId}",
                query.ContestId,
                correlationId);
        }

        return result;
    }
}
