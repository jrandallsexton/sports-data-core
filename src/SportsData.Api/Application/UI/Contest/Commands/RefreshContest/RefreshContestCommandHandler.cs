using SportsData.Core.Common;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.Clients.Contest;

namespace SportsData.Api.Application.UI.Contest.Commands.RefreshContest;

public interface IRefreshContestCommandHandler
{
    Task<Result<Guid>> ExecuteAsync(
        RefreshContestCommand command,
        CancellationToken cancellationToken = default);
}

public class RefreshContestCommandHandler : IRefreshContestCommandHandler
{
    private readonly ILogger<RefreshContestCommandHandler> _logger;
    private readonly IContestClientFactory _contestClientFactory;

    public RefreshContestCommandHandler(
        ILogger<RefreshContestCommandHandler> logger,
        IContestClientFactory contestClientFactory)
    {
        _logger = logger;
        _contestClientFactory = contestClientFactory;
    }

    public async Task<Result<Guid>> ExecuteAsync(
        RefreshContestCommand command,
        CancellationToken cancellationToken = default)
    {
        var correlationId = ActivityExtensions.GetCorrelationId();

        _logger.LogInformation(
            "RefreshContest initiated. ContestId={ContestId}, Sport={Sport}, CorrelationId={CorrelationId}",
            command.ContestId,
            command.Sport,
            correlationId);

        var contestClient = _contestClientFactory.Resolve(command.Sport);
        var result = await contestClient.RefreshContest(command.ContestId, cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogError(
                "RefreshContest failed. ContestId={ContestId}, Sport={Sport}, Status={Status}, CorrelationId={CorrelationId}",
                command.ContestId,
                command.Sport,
                result.Status,
                correlationId);

            var failure = (Failure<bool>)result;
            return new Failure<Guid>(
                correlationId,
                result.Status,
                failure.Errors);
        }

        _logger.LogInformation(
            "RefreshContest completed. ContestId={ContestId}, Sport={Sport}, CorrelationId={CorrelationId}",
            command.ContestId,
            command.Sport,
            correlationId);

        return new Success<Guid>(correlationId, ResultStatus.Accepted);
    }
}
