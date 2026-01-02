using FluentValidation.Results;

using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Api.Infrastructure.Data.Canonical.Models;
using SportsData.Core.Common;

namespace SportsData.Api.Application.Admin.Queries.GetCompetitionsWithoutCompetitors;

public interface IGetCompetitionsWithoutCompetitorsQueryHandler
{
    /// <summary>
        /// Retrieves competitions that have no associated competitors.
        /// </summary>
        /// <param name="query">Parameters controlling which competitions to retrieve.</param>
        /// <returns>
        /// A Result containing a list of CompetitionWithoutCompetitorsDto on success; on failure the Result contains an error status and validation failure details describing the problem.
        /// </returns>
        Task<Result<List<CompetitionWithoutCompetitorsDto>>> ExecuteAsync(
        GetCompetitionsWithoutCompetitorsQuery query, 
        CancellationToken cancellationToken);
}

public class GetCompetitionsWithoutCompetitorsQueryHandler : IGetCompetitionsWithoutCompetitorsQueryHandler
{
    private readonly IProvideCanonicalAdminData _canonicalAdminData;
    private readonly ILogger<GetCompetitionsWithoutCompetitorsQueryHandler> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="GetCompetitionsWithoutCompetitorsQueryHandler"/> with the required dependencies.
    /// </summary>
    public GetCompetitionsWithoutCompetitorsQueryHandler(
        IProvideCanonicalAdminData canonicalAdminData,
        ILogger<GetCompetitionsWithoutCompetitorsQueryHandler> logger)
    {
        _canonicalAdminData = canonicalAdminData;
        _logger = logger;
    }

    /// <summary>
    /// Fetches the list of competitions that have no competitors.
    /// </summary>
    /// <param name="query">Query parameters identifying which competitions to retrieve.</param>
    /// <returns>
    /// A Result containing the retrieved list of <see cref="CompetitionWithoutCompetitorsDto"/>.
    /// On success the result is a success wrapping the list; on failure the result is a failure with an empty list, <see cref="ResultStatus.Error"/>, and a single <see cref="ValidationFailure"/> for the "competitions" key describing the error.
    /// </returns>
    public async Task<Result<List<CompetitionWithoutCompetitorsDto>>> ExecuteAsync(
        GetCompetitionsWithoutCompetitorsQuery query,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _canonicalAdminData.GetCompetitionsWithoutCompetitors(cancellationToken);
            return new Success<List<CompetitionWithoutCompetitorsDto>>(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get competitions without competitors");
            return new Failure<List<CompetitionWithoutCompetitorsDto>>(
                new List<CompetitionWithoutCompetitorsDto>(),
                ResultStatus.Error,
                [new ValidationFailure("competitions", "An error occurred while retrieving competitions without competitors")]);
        }
    }
}