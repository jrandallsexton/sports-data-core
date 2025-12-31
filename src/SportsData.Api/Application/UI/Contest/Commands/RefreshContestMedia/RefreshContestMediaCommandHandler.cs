using FluentValidation.Results;

using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Core.Common;
using SportsData.Core.Extensions;

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
    private readonly IProvideCanonicalData _canonicalDataProvider;

    public RefreshContestMediaCommandHandler(
        ILogger<RefreshContestMediaCommandHandler> logger,
        IProvideCanonicalData canonicalDataProvider)
    {
        _logger = logger;
        _canonicalDataProvider = canonicalDataProvider;
    }

    public async Task<Result<Guid>> ExecuteAsync(
        RefreshContestMediaCommand command,
        CancellationToken cancellationToken = default)
    {
        var correlationId = ActivityExtensions.GetCorrelationId();

        try
        {
            _logger.LogInformation(
                "RefreshContestMedia initiated. ContestId={ContestId}, CorrelationId={CorrelationId}",
                command.ContestId,
                correlationId);

            await _canonicalDataProvider.RefreshContestMediaByContestId(command.ContestId);

            _logger.LogInformation(
                "RefreshContestMedia completed. ContestId={ContestId}, CorrelationId={CorrelationId}",
                command.ContestId,
                correlationId);

            return new Success<Guid>(correlationId, ResultStatus.Accepted);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error refreshing contest media. ContestId={ContestId}, CorrelationId={CorrelationId}",
                command.ContestId,
                correlationId);

            var (status, message) = ex switch
            {
                ArgumentException or ArgumentNullException =>
                    (ResultStatus.Validation, "Invalid request parameters."),
                TimeoutException or HttpRequestException or TaskCanceledException =>
                    (ResultStatus.Error, "The service is temporarily unavailable. Please try again later."),
                InvalidOperationException =>
                    (ResultStatus.BadRequest, "The operation could not be completed."),
                _ =>
                    (ResultStatus.Error, "An error occurred while refreshing contest media.")
            };

            return new Failure<Guid>(
                correlationId,
                status,
                [new ValidationFailure("Error", $"{message} Reference: {correlationId}")]);
        }
    }
}
