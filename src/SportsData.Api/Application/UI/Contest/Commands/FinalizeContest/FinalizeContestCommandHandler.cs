using SportsData.Core.Common;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.Clients.Contest;

namespace SportsData.Api.Application.UI.Contest.Commands.FinalizeContest;

public interface IFinalizeContestCommandHandler
{
    Task<Result<Guid>> ExecuteAsync(
        FinalizeContestCommand command,
        CancellationToken cancellationToken = default);
}

public class FinalizeContestCommandHandler : IFinalizeContestCommandHandler
{
    private readonly ILogger<FinalizeContestCommandHandler> _logger;
    private readonly IContestClientFactory _contestClientFactory;

    public FinalizeContestCommandHandler(
        ILogger<FinalizeContestCommandHandler> logger,
        IContestClientFactory contestClientFactory)
    {
        _logger = logger;
        _contestClientFactory = contestClientFactory;
    }

    public async Task<Result<Guid>> ExecuteAsync(
        FinalizeContestCommand command,
        CancellationToken cancellationToken = default)
    {
        var correlationId = ActivityExtensions.GetCorrelationId();

        _logger.LogInformation(
            "FinalizeContest initiated. ContestId={ContestId}, Sport={Sport}, CorrelationId={CorrelationId}",
            command.ContestId,
            command.Sport,
            correlationId);

        var contestClient = _contestClientFactory.Resolve(command.Sport);
        var result = await contestClient.FinalizeContestByContestId(command.ContestId, cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogError(
                "FinalizeContest failed. ContestId={ContestId}, Sport={Sport}, Status={Status}, CorrelationId={CorrelationId}",
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
            "FinalizeContest completed. ContestId={ContestId}, Sport={Sport}, CorrelationId={CorrelationId}",
            command.ContestId,
            command.Sport,
            correlationId);

        return new Success<Guid>(correlationId, ResultStatus.Accepted);
    }
}
