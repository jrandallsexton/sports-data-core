using FluentValidation.Results;

using SportsData.Core.Common;
using SportsData.Core.Common.Mapping;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.Clients.Contest;

namespace SportsData.Api.Application.UI.Contest.Commands.RefreshContestMedia;

public interface IRefreshContestMediaCommandHandler
{
    Task<Result<Guid>> ExecuteAsync(
        RefreshContestMediaCommand command,
        CancellationToken cancellationToken = default);
}

public class RefreshContestMediaCommandHandler : IRefreshContestMediaCommandHandler
{
    private readonly ILogger<RefreshContestMediaCommandHandler> _logger;
    private readonly IContestClientFactory _contestClientFactory;

    public RefreshContestMediaCommandHandler(
        ILogger<RefreshContestMediaCommandHandler> logger,
        IContestClientFactory contestClientFactory)
    {
        _logger = logger;
        _contestClientFactory = contestClientFactory;
    }

    public async Task<Result<Guid>> ExecuteAsync(
        RefreshContestMediaCommand command,
        CancellationToken cancellationToken = default)
    {
        var correlationId = ActivityExtensions.GetCorrelationId();

        _logger.LogInformation(
            "RefreshContestMedia initiated. ContestId={ContestId}, Sport={Sport}, CorrelationId={CorrelationId}",
            command.ContestId,
            command.Sport,
            correlationId);

        var client = _contestClientFactory.Resolve(command.Sport);
        var result = await client.RefreshContestMediaByContestId(command.ContestId, cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogError(
                "RefreshContestMedia failed. ContestId={ContestId}, Status={Status}, CorrelationId={CorrelationId}",
                command.ContestId,
                result.Status,
                correlationId);

            var failure = (Failure<bool>)result;
            return new Failure<Guid>(
                correlationId,
                result.Status,
                failure.Errors);
        }

        _logger.LogInformation(
            "RefreshContestMedia completed. ContestId={ContestId}, CorrelationId={CorrelationId}",
            command.ContestId,
            correlationId);

        return new Success<Guid>(correlationId, ResultStatus.Accepted);
    }
}
