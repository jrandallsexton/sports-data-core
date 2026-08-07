using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;

namespace SportsData.Api.Application.Admin.Queries.GetMatchupPreviewCaptures;

public interface IGetMatchupPreviewCapturesQueryHandler
{
    Task<Result<List<MatchupPreviewCaptureDto>>> ExecuteAsync(GetMatchupPreviewCapturesQuery query, CancellationToken cancellationToken);
}

public class GetMatchupPreviewCapturesQueryHandler : IGetMatchupPreviewCapturesQueryHandler
{
    private readonly AppDataContext _dataContext;
    private readonly ILogger<GetMatchupPreviewCapturesQueryHandler> _logger;

    public GetMatchupPreviewCapturesQueryHandler(
        AppDataContext dataContext,
        ILogger<GetMatchupPreviewCapturesQueryHandler> logger)
    {
        _dataContext = dataContext;
        _logger = logger;
    }

    public async Task<Result<List<MatchupPreviewCaptureDto>>> ExecuteAsync(
        GetMatchupPreviewCapturesQuery query,
        CancellationToken cancellationToken)
    {
        var validation = await new GetMatchupPreviewCapturesQueryValidator()
            .ValidateAsync(query, cancellationToken);

        if (!validation.IsValid)
        {
            return new Failure<List<MatchupPreviewCaptureDto>>(
                default!,
                ResultStatus.Validation,
                validation.Errors);
        }

        try
        {
            var captures = await _dataContext.MatchupPreviewPrompts
                .AsNoTracking()
                .Where(x => x.ContestId == query.ContestId)
                .OrderByDescending(x => x.CreatedUtc)
                .Select(x => new MatchupPreviewCaptureDto
                {
                    Id = x.Id,
                    ContestId = x.ContestId,
                    Sport = x.Sport,
                    MatchupPreviewId = x.MatchupPreviewId,
                    PromptVersion = x.PromptVersion,
                    PayloadJson = x.PayloadJson,
                    EditorNote = x.EditorNote,
                    CharCount = x.CharCount,
                    EstTokens = x.EstTokens,
                    Mode = x.Mode,
                    Model = x.Model,
                    RawResponse = x.RawResponse,
                    ResponseValidationErrors = x.ResponseValidationErrors,
                    // The instruction text is stored per capture, so this is
                    // the EXACT model input — no blob round-trip, immune to
                    // in-place blob edits after the fact.
                    FullPrompt = x.EditorNote != null
                        ? x.PromptText + "\n\n" + x.PayloadJson + "\n\nAdditional feedback from the editor:\n\"" + x.EditorNote + "\""
                        : x.PromptText + "\n\n" + x.PayloadJson,
                    CreatedUtc = x.CreatedUtc
                })
                .ToListAsync(cancellationToken);

            // Zero captures is a normal empty state — return 200 + [] so a
            // 404 from this route can only mean the endpoint isn't there.
            return new Success<List<MatchupPreviewCaptureDto>>(captures);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving prompt captures for contest {ContestId}", query.ContestId);
            return new Failure<List<MatchupPreviewCaptureDto>>(
                default!,
                ResultStatus.Error,
                [new FluentValidation.Results.ValidationFailure("Error", "An error occurred while retrieving prompt captures")]);
        }
    }
}
