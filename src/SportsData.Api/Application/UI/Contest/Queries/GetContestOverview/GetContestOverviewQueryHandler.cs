using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.Clients.Contest;

namespace SportsData.Api.Application.UI.Contest.Queries.GetContestOverview;

public interface IGetContestOverviewQueryHandler
{
    Task<Result<ContestOverviewDto>> ExecuteAsync(
        GetContestOverviewQuery query,
        CancellationToken cancellationToken = default);
}

public class GetContestOverviewQueryHandler : IGetContestOverviewQueryHandler
{
    private readonly ILogger<GetContestOverviewQueryHandler> _logger;
    private readonly IContestClientFactory _contestClientFactory;

    public GetContestOverviewQueryHandler(
        ILogger<GetContestOverviewQueryHandler> logger,
        IContestClientFactory contestClientFactory)
    {
        _logger = logger;
        _contestClientFactory = contestClientFactory;
    }

    public async Task<Result<ContestOverviewDto>> ExecuteAsync(
        GetContestOverviewQuery query,
        CancellationToken cancellationToken = default)
    {
        var contestClient = _contestClientFactory.Resolve(query.Sport);

        var correlationId = ActivityExtensions.GetCorrelationId();

        _logger.LogInformation(
            "GetContestOverview requested. ContestId={ContestId}, CorrelationId={CorrelationId}",
            query.ContestId,
            correlationId);

        var result = await contestClient.GetContestOverviewByContestId(query.ContestId, cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogWarning(
                "Failed to get contest overview. ContestId={ContestId}, CorrelationId={CorrelationId}",
                query.ContestId,
                correlationId);
        }

        return result;
    }
}
