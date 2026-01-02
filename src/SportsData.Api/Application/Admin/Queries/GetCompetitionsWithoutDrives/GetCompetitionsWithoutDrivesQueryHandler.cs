using FluentValidation.Results;

using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Api.Infrastructure.Data.Canonical.Models;
using SportsData.Core.Common;

namespace SportsData.Api.Application.Admin.Queries.GetCompetitionsWithoutDrives;

public interface IGetCompetitionsWithoutDrivesQueryHandler
{
    /// <summary>
        /// Retrieves competitions that have no drives.
        /// </summary>
        /// <param name="query">The query containing request parameters for retrieving competitions without drives.</param>
        /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
        /// <returns>A Result containing a list of CompetitionWithoutDrivesDto on success, or failure details on error.</returns>
        Task<Result<List<CompetitionWithoutDrivesDto>>> ExecuteAsync(
        GetCompetitionsWithoutDrivesQuery query,
        CancellationToken cancellationToken);
}

public class GetCompetitionsWithoutDrivesQueryHandler : IGetCompetitionsWithoutDrivesQueryHandler
{
    private readonly IProvideCanonicalAdminData _canonicalAdminData;
    private readonly ILogger<GetCompetitionsWithoutDrivesQueryHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetCompetitionsWithoutDrivesQueryHandler"/> class.
    /// </summary>
    /// <param name="canonicalAdminData">Provides access to canonical administrative data for fetching competitions without drives.</param>
    /// <param name="logger">Logger for recording operational and error information within the handler.</param>
    public GetCompetitionsWithoutDrivesQueryHandler(
        IProvideCanonicalAdminData canonicalAdminData,
        ILogger<GetCompetitionsWithoutDrivesQueryHandler> logger)
    {
        _canonicalAdminData = canonicalAdminData;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves competitions that do not have drives from the canonical admin data store.
    /// </summary>
    /// <param name="query">Query parameters describing which competitions to retrieve.</param>
    /// <param name="cancellationToken">Token to observe while waiting for the operation to complete.</param>
    /// <returns>A Result containing the list of competitions without drives on success; on failure, a Result with an empty list, ResultStatus.Error, and a validation failure describing the retrieval error.</returns>
    public async Task<Result<List<CompetitionWithoutDrivesDto>>> ExecuteAsync(
        GetCompetitionsWithoutDrivesQuery query,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _canonicalAdminData.GetCompetitionsWithoutDrives(cancellationToken);
            return new Success<List<CompetitionWithoutDrivesDto>>(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get competitions without drives");
            return new Failure<List<CompetitionWithoutDrivesDto>>(
                new List<CompetitionWithoutDrivesDto>(),
                ResultStatus.Error,
                [new ValidationFailure("competitions", "An error occurred while retrieving competitions without drives")]);
        }
    }
}