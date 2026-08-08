using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;

namespace SportsData.Api.Application.Admin.Prompts;

public interface ISetDefaultPromptCommandHandler
{
    Task<Result<Guid>> ExecuteAsync(Guid promptId, CancellationToken cancellationToken);
}

/// <summary>
/// Makes a prompt the default for ITS OWN (Type, Sport, WithStats) slot,
/// clearing the slot's previous default. Takes effect on the next
/// generation run — no deploy.
/// </summary>
public class SetDefaultPromptCommandHandler : ISetDefaultPromptCommandHandler
{
    private readonly AppDataContext _dataContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<SetDefaultPromptCommandHandler> _logger;

    public SetDefaultPromptCommandHandler(
        AppDataContext dataContext,
        IDateTimeProvider dateTimeProvider,
        ILogger<SetDefaultPromptCommandHandler> logger)
    {
        _dataContext = dataContext;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<Result<Guid>> ExecuteAsync(Guid promptId, CancellationToken cancellationToken)
    {
        var prompt = await _dataContext.Prompts
            .FirstOrDefaultAsync(p => p.Id == promptId, cancellationToken);

        if (prompt is null)
        {
            return new Failure<Guid>(
                default!,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(promptId), "Prompt not found")]);
        }

        if (!prompt.IsDefault)
        {
            var currentDefaults = await _dataContext.Prompts
                .Where(p => p.Id != prompt.Id
                         && p.Type == prompt.Type
                         && p.IsDefault
                         && p.WithStats == prompt.WithStats
                         && p.Sport == prompt.Sport)
                .ToListAsync(cancellationToken);

            foreach (var existing in currentDefaults)
            {
                existing.IsDefault = false;
                existing.ModifiedUtc = _dateTimeProvider.UtcNow();
            }

            prompt.IsDefault = true;
            prompt.ModifiedUtc = _dateTimeProvider.UtcNow();

            try
            {
                // Clear + promote commit in ONE SaveChanges (single
                // transaction); the partial unique indexes turn a concurrent
                // promotion race into a constraint violation here.
                await _dataContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogWarning(ex, "Concurrent default-slot change detected while promoting prompt {PromptId}.", prompt.Id);
                return new Failure<Guid>(
                    default!,
                    ResultStatus.BadRequest,
                    [new ValidationFailure(nameof(promptId), "The slot's default changed concurrently — reload and retry.")]);
            }

            _logger.LogInformation(
                "Prompt {PromptId} ('{Name}') is now the default for slot (Sport: {Sport}, WithStats: {WithStats}); {Cleared} previous default(s) cleared.",
                prompt.Id, LogSanitizer.Clean(prompt.Name), prompt.Sport, prompt.WithStats, currentDefaults.Count);
        }

        return new Success<Guid>(prompt.Id);
    }
}
