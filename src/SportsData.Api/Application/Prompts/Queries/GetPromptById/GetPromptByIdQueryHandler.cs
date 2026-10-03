using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Api.Application.Prompts.Queries.GetPrompts;
using SportsData.Core.Common;

namespace SportsData.Api.Application.Prompts.Queries.GetPromptById;

public class PromptDetailDto : PromptSummaryDto
{
    public string Text { get; set; } = default!;
}

public interface IGetPromptByIdQueryHandler
{
    Task<Result<PromptDetailDto>> ExecuteAsync(Guid promptId, CancellationToken cancellationToken);
}

public class GetPromptByIdQueryHandler : IGetPromptByIdQueryHandler
{
    private readonly AppDataContext _dataContext;

    public GetPromptByIdQueryHandler(AppDataContext dataContext)
    {
        _dataContext = dataContext;
    }

    public async Task<Result<PromptDetailDto>> ExecuteAsync(Guid promptId, CancellationToken cancellationToken)
    {
        var prompt = await _dataContext.Prompts
            .AsNoTracking()
            .Where(p => p.Id == promptId)
            .Select(p => new PromptDetailDto
            {
                Id = p.Id,
                Name = p.Name,
                Type = p.Type,
                Sport = p.Sport,
                WithStats = p.WithStats,
                IsDefault = p.IsDefault,
                Description = p.Description,
                TextLength = p.Text.Length,
                Text = p.Text,
                UsedByPreviewCount = _dataContext.MatchupPreviews.Count(mp => mp.PromptId == p.Id),
                CreatedUtc = p.CreatedUtc
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (prompt is null)
        {
            return new Failure<PromptDetailDto>(
                default!,
                ResultStatus.NotFound,
                [new FluentValidation.Results.ValidationFailure(nameof(promptId), "Prompt not found")]);
        }

        return new Success<PromptDetailDto>(prompt);
    }
}
