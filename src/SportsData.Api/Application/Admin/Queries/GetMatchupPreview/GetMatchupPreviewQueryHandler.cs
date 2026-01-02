using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;
using SportsData.Core.Extensions;

namespace SportsData.Api.Application.Admin.Queries.GetMatchupPreview;

public interface IGetMatchupPreviewQueryHandler
{
    Task<Result<string>> ExecuteAsync(GetMatchupPreviewQuery query, CancellationToken cancellationToken);
}

public class GetMatchupPreviewQueryHandler : IGetMatchupPreviewQueryHandler
{
    private readonly AppDataContext _dataContext;
    private readonly ILogger<GetMatchupPreviewQueryHandler> _logger;

    public GetMatchupPreviewQueryHandler(
        AppDataContext dataContext,
        ILogger<GetMatchupPreviewQueryHandler> logger)
    {
        _dataContext = dataContext;
        _logger = logger;
    }

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
