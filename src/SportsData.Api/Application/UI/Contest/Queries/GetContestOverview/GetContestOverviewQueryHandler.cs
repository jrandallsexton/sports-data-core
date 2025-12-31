using FluentValidation.Results;

using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Extensions;

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
    private readonly IProvideCanonicalData _canonicalDataProvider;

    public GetContestOverviewQueryHandler(
        ILogger<GetContestOverviewQueryHandler> logger,
        IProvideCanonicalData canonicalDataProvider)
    {
        _logger = logger;
        _canonicalDataProvider = canonicalDataProvider;
    }

    public async Task<Result<ContestOverviewDto>> ExecuteAsync(
        GetContestOverviewQuery query,
        CancellationToken cancellationToken = default)
    {
        var correlationId = ActivityExtensions.GetCorrelationId();

        _logger.LogInformation(
            "GetContestOverview requested. ContestId={ContestId}, CorrelationId={CorrelationId}",
            query.ContestId,
            correlationId);

        var result = await _canonicalDataProvider.GetContestOverviewByContestId(query.ContestId);

        if (result == null)
        {
            _logger.LogWarning(
                "Contest overview not found. ContestId={ContestId}, CorrelationId={CorrelationId}",
                query.ContestId,
                correlationId);
            return new Failure<ContestOverviewDto>(
                default!,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(query.ContestId), $"Contest with ID {query.ContestId} not found")]);
        }

        return new Success<ContestOverviewDto>(result);
    }
}
