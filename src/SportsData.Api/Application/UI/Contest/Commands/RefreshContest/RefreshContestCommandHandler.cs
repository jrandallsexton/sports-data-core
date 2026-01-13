using FluentValidation.Results;

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

        try
        {
            _logger.LogInformation(
                "RefreshContest initiated. ContestId={ContestId}, Sport={Sport}, CorrelationId={CorrelationId}",
                command.ContestId,
                command.Sport,
                correlationId);

            var contestClient = _contestClientFactory.Resolve(command.Sport);
            await contestClient.RefreshContest(command.ContestId, cancellationToken);

            _logger.LogInformation(
                "RefreshContest completed. ContestId={ContestId}, Sport={Sport}, CorrelationId={CorrelationId}",
                command.ContestId,
                command.Sport,
                correlationId);

            return new Success<Guid>(correlationId, ResultStatus.Accepted);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error refreshing contest. ContestId={ContestId}, Sport={Sport}, CorrelationId={CorrelationId}",
                command.ContestId,
                command.Sport,
                correlationId);
            return new Failure<Guid>(
                default,
                ResultStatus.BadRequest,
                [new ValidationFailure("Error", ex.Message)]);
        }
    }
}
