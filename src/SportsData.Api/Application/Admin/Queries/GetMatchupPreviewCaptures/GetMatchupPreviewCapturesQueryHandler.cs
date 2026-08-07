using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Prompts;
using SportsData.Core.Common;

namespace SportsData.Api.Application.Admin.Queries.GetMatchupPreviewCaptures;

public interface IGetMatchupPreviewCapturesQueryHandler
{
    Task<Result<List<MatchupPreviewCaptureDto>>> ExecuteAsync(GetMatchupPreviewCapturesQuery query, CancellationToken cancellationToken);
}

public class GetMatchupPreviewCapturesQueryHandler : IGetMatchupPreviewCapturesQueryHandler
{
    private readonly AppDataContext _dataContext;
    private readonly MatchupPreviewPromptProvider _promptProvider;
    private readonly ILogger<GetMatchupPreviewCapturesQueryHandler> _logger;

    public GetMatchupPreviewCapturesQueryHandler(
        AppDataContext dataContext,
        MatchupPreviewPromptProvider promptProvider,
        ILogger<GetMatchupPreviewCapturesQueryHandler> logger)
    {
        _dataContext = dataContext;
        _promptProvider = promptProvider;
        _logger = logger;
    }

    public async Task<Result<List<MatchupPreviewCaptureDto>>> ExecuteAsync(
        GetMatchupPreviewCapturesQuery query,
        CancellationToken cancellationToken)
    {
        if (query.ContestId == Guid.Empty)
        {
            return new Failure<List<MatchupPreviewCaptureDto>>(
                default!,
                ResultStatus.Validation,
                [new ValidationFailure(nameof(query.ContestId), "Contest ID cannot be empty")]);
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
                    CreatedUtc = x.CreatedUtc
                })
                .ToListAsync(cancellationToken);

            // Zero captures is a normal empty state — return 200 + [] so a
            // 404 from this route can only mean the endpoint isn't there.
            foreach (var capture in captures)
            {
                // Reconstruct exactly what the processor renders:
                // promptText + "\n\n" + payload [+ editor note]
                var promptText = await _promptProvider.GetPromptTextByVersionAsync(capture.PromptVersion);

                var editorNote = capture.EditorNote != null
                    ? $"\n\nAdditional feedback from the editor:\n\"{capture.EditorNote}\""
                    : string.Empty;

                capture.FullPrompt = $"{promptText}\n\n{capture.PayloadJson}{editorNote}";
            }

            return new Success<List<MatchupPreviewCaptureDto>>(captures);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving prompt captures for contest {ContestId}", query.ContestId);
            return new Failure<List<MatchupPreviewCaptureDto>>(
                default!,
                ResultStatus.Error,
                [new ValidationFailure("Error", "An error occurred while retrieving prompt captures")]);
        }
    }
}
