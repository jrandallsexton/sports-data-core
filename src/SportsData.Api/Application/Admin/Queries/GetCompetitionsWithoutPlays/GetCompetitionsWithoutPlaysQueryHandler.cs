using FluentValidation.Results;

using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Api.Infrastructure.Data.Canonical.Models;
using SportsData.Core.Common;

namespace SportsData.Api.Application.Admin.Queries.GetCompetitionsWithoutPlays;

public interface IGetCompetitionsWithoutPlaysQueryHandler
{
    /// <summary>
        /// Retrieves competitions that have no recorded plays.
        /// </summary>
        /// <param name="query">Parameters that control which competitions without plays to retrieve (e.g., filters or pagination).</param>
        /// <param name="cancellationToken">Token to observe while waiting for the operation to complete.</param>
        /// <returns>A Result containing a list of CompetitionWithoutPlaysDto when successful, or a Failure with error details.</returns>
        Task<Result<List<CompetitionWithoutPlaysDto>>> ExecuteAsync(
        GetCompetitionsWithoutPlaysQuery query,
        CancellationToken cancellationToken);
}

public class GetCompetitionsWithoutPlaysQueryHandler : IGetCompetitionsWithoutPlaysQueryHandler
{
    private readonly IProvideCanonicalAdminData _canonicalAdminData;
    private readonly ILogger<GetCompetitionsWithoutPlaysQueryHandler> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="GetCompetitionsWithoutPlaysQueryHandler"/> with its required dependencies.
    /// </summary>
    /// <param name="canonicalAdminData">Provider used to retrieve canonical admin competition data, including competitions without plays.</param>
    /// <param name="logger">Logger for recording operational events and errors within the handler.</param>
    public GetCompetitionsWithoutPlaysQueryHandler(
        IProvideCanonicalAdminData canonicalAdminData,
        ILogger<GetCompetitionsWithoutPlaysQueryHandler> logger)
    {
        _canonicalAdminData = canonicalAdminData;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves competitions that have no plays.
    /// </summary>
    /// <param name="query">Query object specifying retrieval options for competitions without plays.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A Result containing a list of <see cref="CompetitionWithoutPlaysDto"/> when successful; on failure a Result with an empty list, <see cref="ResultStatus.Error"/>, and a validation failure for "competitions".</returns>
    public async Task<Result<List<CompetitionWithoutPlaysDto>>> ExecuteAsync(
        GetCompetitionsWithoutPlaysQuery query,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _canonicalAdminData.GetCompetitionsWithoutPlays(cancellationToken);
            return new Success<List<CompetitionWithoutPlaysDto>>(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get competitions without plays");
            return new Failure<List<CompetitionWithoutPlaysDto>>(
                new List<CompetitionWithoutPlaysDto>(),
                ResultStatus.Error,
                [new ValidationFailure("competitions", "An error occurred while retrieving competitions without plays")]);
        }
    }
}