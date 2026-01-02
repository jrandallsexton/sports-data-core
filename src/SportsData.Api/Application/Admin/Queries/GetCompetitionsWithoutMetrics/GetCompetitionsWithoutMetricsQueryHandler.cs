using FluentValidation.Results;

using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Api.Infrastructure.Data.Canonical.Models;
using SportsData.Core.Common;

namespace SportsData.Api.Application.Admin.Queries.GetCompetitionsWithoutMetrics;

public interface IGetCompetitionsWithoutMetricsQueryHandler
{
    /// <summary>
        /// Retrieves competitions that do not have associated metrics.
        /// </summary>
        /// <param name="query">Query object containing retrieval options for competitions without metrics.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// A Result containing the list of <see cref="CompetitionWithoutMetricsDto"/>.
        /// On success the Result holds the competitions; on failure the Result indicates an error and includes validation failures describing the problem.
        /// </returns>
        Task<Result<List<CompetitionWithoutMetricsDto>>> ExecuteAsync(
        GetCompetitionsWithoutMetricsQuery query,
        CancellationToken cancellationToken);
}

public class GetCompetitionsWithoutMetricsQueryHandler : IGetCompetitionsWithoutMetricsQueryHandler
{
    private readonly IProvideCanonicalAdminData _canonicalAdminData;
    private readonly ILogger<GetCompetitionsWithoutMetricsQueryHandler> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="GetCompetitionsWithoutMetricsQueryHandler"/> with required dependencies.
    /// </summary>
    public GetCompetitionsWithoutMetricsQueryHandler(
        IProvideCanonicalAdminData canonicalAdminData,
        ILogger<GetCompetitionsWithoutMetricsQueryHandler> logger)
    {
        _canonicalAdminData = canonicalAdminData;
        _logger = logger;
    }

    /// <summary>
    /// Handles the query to retrieve competitions that do not have metrics.
    /// </summary>
    /// <param name="query">The query parameters for retrieving competitions without metrics.</param>
    /// <param name="cancellationToken">Token to observe while waiting for the operation to complete.</param>
    /// <returns>
    /// A Result wrapping the retrieved competitions: on success contains the list of CompetitionWithoutMetricsDto; on error contains an empty list, an error status, and a validation failure for "competitions".
    /// </returns>
    public async Task<Result<List<CompetitionWithoutMetricsDto>>> ExecuteAsync(
        GetCompetitionsWithoutMetricsQuery query,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _canonicalAdminData.GetCompetitionsWithoutMetrics(cancellationToken);
            return new Success<List<CompetitionWithoutMetricsDto>>(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get competitions without metrics");
            return new Failure<List<CompetitionWithoutMetricsDto>>(
                new List<CompetitionWithoutMetricsDto>(),
                ResultStatus.Error,
                [new ValidationFailure("competitions", "An error occurred while retrieving competitions without metrics")]);
        }
    }
}