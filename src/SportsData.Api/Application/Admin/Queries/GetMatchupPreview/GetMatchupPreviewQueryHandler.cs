using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;
using SportsData.Core.Extensions;

namespace SportsData.Api.Application.Admin.Queries.GetMatchupPreview;

public interface IGetMatchupPreviewQueryHandler
{
    /// <summary>
/// Retrieves the matchup preview JSON for the contest specified by the query.
/// </summary>
/// <param name="query">Query containing the ContestId of the matchup preview to retrieve.</param>
/// <returns>
/// A Result containing the preview as a JSON string when successful; a failure Result containing validation failures if the ContestId is empty, a NotFound failure if no preview exists for the specified contest, or an Error failure if an exception occurs during retrieval.
/// </returns>
Task<Result<string>> ExecuteAsync(GetMatchupPreviewQuery query, CancellationToken cancellationToken);
}

public class GetMatchupPreviewQueryHandler : IGetMatchupPreviewQueryHandler
{
    private readonly AppDataContext _dataContext;
    private readonly ILogger<GetMatchupPreviewQueryHandler> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="GetMatchupPreviewQueryHandler"/> with the required dependencies.
    /// </summary>
    public GetMatchupPreviewQueryHandler(
        AppDataContext dataContext,
        ILogger<GetMatchupPreviewQueryHandler> logger)
    {
        _dataContext = dataContext;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves the matchup preview JSON for the specified contest.
    /// </summary>
    /// <param name="query">Query containing the ContestId of the matchup preview to retrieve.</param>
    /// <returns>
    /// A Result containing:
    /// - on success: the preview JSON string;
    /// - on validation failure: failure with ResultStatus.Validation when ContestId is empty;
    /// - on not found: failure with ResultStatus.NotFound when no preview exists for the ContestId;
    /// - on error: failure with ResultStatus.Error when an exception occurs during retrieval.
    /// </returns>
    public async Task<Result<string>> ExecuteAsync(GetMatchupPreviewQuery query, CancellationToken cancellationToken)
    {
        if (query.ContestId == Guid.Empty)
        {
            return new Failure<string>(
                default!,
                ResultStatus.Validation,
                [new ValidationFailure(nameof(query.ContestId), "Contest ID cannot be empty")]);
        }

        try
        {
            var preview = await _dataContext.MatchupPreviews
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.ContestId == query.ContestId, cancellationToken);

            if (preview is null)
            {
                _logger.LogWarning("No preview found for contest {ContestId}", query.ContestId);
                return new Failure<string>(
                    default!,
                    ResultStatus.NotFound,
                    [new ValidationFailure(nameof(query.ContestId), "No preview found for the specified contest")]);
            }

            return new Success<string>(preview.ToJson());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving matchup preview for contest {ContestId}", query.ContestId);
            return new Failure<string>(
                string.Empty,
                ResultStatus.Error,
                [new ValidationFailure("Error", "An error occurred while retrieving the matchup preview")]);
        }
    }
}