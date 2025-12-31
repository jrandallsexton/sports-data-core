using FluentValidation.Results;

using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Core.Common;
using SportsData.Core.Extensions;

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
    private readonly IProvideCanonicalData _canonicalDataProvider;

    public RefreshContestCommandHandler(
        ILogger<RefreshContestCommandHandler> logger,
        IProvideCanonicalData canonicalDataProvider)
    {
        _logger = logger;
        _canonicalDataProvider = canonicalDataProvider;
    }

    public async Task<Result<Guid>> ExecuteAsync(
        RefreshContestCommand command,
        CancellationToken cancellationToken = default)
    {
        var correlationId = ActivityExtensions.GetCorrelationId();

        try
        {
            _logger.LogInformation(
                "RefreshContest initiated. ContestId={ContestId}, CorrelationId={CorrelationId}",
                command.ContestId,
                correlationId);

            await _canonicalDataProvider.RefreshContestByContestId(command.ContestId);

            _logger.LogInformation(
                "RefreshContest completed. ContestId={ContestId}, CorrelationId={CorrelationId}",
                command.ContestId,
                correlationId);

            return new Success<Guid>(correlationId, ResultStatus.Accepted);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error refreshing contest. ContestId={ContestId}, CorrelationId={CorrelationId}",
                command.ContestId,
                correlationId);
            return new Failure<Guid>(
                default,
                ResultStatus.BadRequest,
                [new ValidationFailure("Error", ex.Message)]);
        }
    }
}
