using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

namespace SportsData.Api.Application.Admin.Prompts;

/// <summary>List row — metadata only; fetch a single prompt for the text.</summary>
public class PromptSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public PromptType Type { get; set; }
    public Sport? Sport { get; set; }
    public bool WithStats { get; set; }
    public bool IsDefault { get; set; }
    public string? Description { get; set; }
    public int TextLength { get; set; }

    /// <summary>Real previews generated with this prompt. Non-zero = the prompt is immutable.</summary>
    public int UsedByPreviewCount { get; set; }

    public DateTime CreatedUtc { get; set; }
}

public class PromptDetailDto : PromptSummaryDto
{
    public string Text { get; set; } = default!;
}

public interface IGetPromptsQueryHandler
{
    Task<Result<List<PromptSummaryDto>>> ExecuteAsync(CancellationToken cancellationToken);
}

public interface IGetPromptByIdQueryHandler
{
    Task<Result<PromptDetailDto>> ExecuteAsync(Guid promptId, CancellationToken cancellationToken);
}

public class GetPromptsQueryHandler : IGetPromptsQueryHandler
{
    private readonly AppDataContext _dataContext;

    public GetPromptsQueryHandler(AppDataContext dataContext)
    {
        _dataContext = dataContext;
    }

    public async Task<Result<List<PromptSummaryDto>>> ExecuteAsync(CancellationToken cancellationToken)
    {
        var prompts = await _dataContext.Prompts
            .AsNoTracking()
            .OrderByDescending(p => p.IsDefault)
            .ThenBy(p => p.Name)
            .Select(p => new PromptSummaryDto
            {
                Id = p.Id,
                Name = p.Name,
                Type = p.Type,
                Sport = p.Sport,
                WithStats = p.WithStats,
                IsDefault = p.IsDefault,
                Description = p.Description,
                TextLength = p.Text.Length,
                UsedByPreviewCount = _dataContext.MatchupPreviews.Count(mp => mp.PromptId == p.Id),
                CreatedUtc = p.CreatedUtc
            })
            .ToListAsync(cancellationToken);

        return new Success<List<PromptSummaryDto>>(prompts);
    }
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
