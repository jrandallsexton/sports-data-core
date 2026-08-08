using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

namespace SportsData.Api.Infrastructure.Prompts;

/// <summary>
/// What the preview pipeline needs to resolve prompt instructions.
/// </summary>
/// <param name="Sport">Drives sport-specific default resolution.</param>
/// <param name="HasStats">Selects the with-stats vs no-stats default slot.</param>
/// <param name="PromptId">
/// Explicit Prompt entity override for Preview Lab experiments — when set,
/// default resolution is bypassed entirely.
/// </param>
public sealed record PreviewPromptRequest(Sport Sport, bool HasStats, Guid? PromptId = null);

public sealed record PreviewPrompt(Guid PromptId, string PromptText, string PromptName);

public interface IMatchupPreviewPromptProvider
{
    /// <summary>
    /// Resolve prompt instructions. Throws when an explicit PromptId does
    /// not exist (an operator typo must fail loudly, never silently fall
    /// back to a default) or when no default is configured for the slot.
    /// </summary>
    Task<PreviewPrompt> GetPromptAsync(PreviewPromptRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Database-backed prompt resolution. Prompt text lives in the (private)
/// API database as first-class Prompt entities — no blob storage, no
/// Azurite for local dev, and the coming management UI is plain CRUD.
///
/// Resolution order:
/// 1. Explicit <c>PromptId</c> → that Prompt row (must be a MatchupPreview
///    prompt), no fallback.
/// 2. Default for the slot (Type=MatchupPreview, WithStats, Sport):
///    a sport-specific default outranks a Sport=null (any-sport) default.
///    Creating/flipping defaults takes effect on the next run — no deploy.
///
/// No caching: a scoped indexed read per generation is noise, and its
/// absence removes the fault-eviction/staleness machinery the blob era
/// needed.
/// </summary>
public class MatchupPreviewPromptProvider : IMatchupPreviewPromptProvider
{
    private readonly AppDataContext _dataContext;

    public MatchupPreviewPromptProvider(AppDataContext dataContext)
    {
        _dataContext = dataContext;
    }

    public async Task<PreviewPrompt> GetPromptAsync(PreviewPromptRequest request, CancellationToken cancellationToken = default)
    {
        if (request.PromptId is { } promptId)
        {
            var prompt = await _dataContext.Prompts
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == promptId, cancellationToken)
                ?? throw new InvalidOperationException(
                    $"Prompt {promptId} does not exist. Experiment overrides fail loudly rather than falling back to a default.");

            if (prompt.Type != PromptType.MatchupPreview)
                throw new InvalidOperationException(
                    $"Prompt {promptId} ('{prompt.Name}') is a {prompt.Type} prompt, not a MatchupPreview prompt.");

            return new PreviewPrompt(prompt.Id!, prompt.Text, prompt.Name);
        }

        var resolved = await _dataContext.Prompts
            .AsNoTracking()
            .Where(p => p.Type == PromptType.MatchupPreview
                     && p.IsDefault
                     && p.WithStats == request.HasStats
                     && (p.Sport == request.Sport || p.Sport == null))
            .OrderByDescending(p => p.Sport != null) // sport-specific outranks any-sport
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                $"No default matchup-preview prompt configured for Sport={request.Sport}, WithStats={request.HasStats}. " +
                "Seed one via POST /admin/prompts or POST /admin/prompts/import-blob.");

        return new PreviewPrompt(resolved.Id!, resolved.Text, resolved.Name);
    }
}
