using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

namespace SportsData.Api.Application.Admin.Prompts;

public interface ICreatePromptCommandHandler
{
    Task<Result<Guid>> ExecuteAsync(CreatePromptCommand command, CancellationToken cancellationToken);
}

public class CreatePromptCommandHandler : ICreatePromptCommandHandler
{
    private readonly AppDataContext _dataContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<CreatePromptCommandHandler> _logger;

    public CreatePromptCommandHandler(
        AppDataContext dataContext,
        IDateTimeProvider dateTimeProvider,
        ILogger<CreatePromptCommandHandler> logger)
    {
        _dataContext = dataContext;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<Result<Guid>> ExecuteAsync(CreatePromptCommand command, CancellationToken cancellationToken)
    {
        var validation = await new CreatePromptCommandValidator().ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return new Failure<Guid>(default!, ResultStatus.Validation, validation.Errors);
        }

        var name = command.Name.Trim();

        var nameTaken = await _dataContext.Prompts
            .AsNoTracking()
            .AnyAsync(p => p.Name == name, cancellationToken);

        if (nameTaken)
        {
            return new Failure<Guid>(
                default!,
                ResultStatus.Validation,
                [new ValidationFailure(nameof(command.Name), $"A prompt named '{name}' already exists. Prompt names are immutable provenance — create a new version name instead.")]);
        }

        if (command.IsDefault)
        {
            // At most one default per (Type, Sport, WithStats) slot. Note:
            // a sport-specific default coexists with the any-sport default
            // by design (resolution prefers sport-specific).
            var currentDefaults = await _dataContext.Prompts
                .Where(p => p.Type == PromptType.MatchupPreview
                         && p.IsDefault
                         && p.WithStats == command.WithStats
                         && p.Sport == command.Sport)
                .ToListAsync(cancellationToken);

            foreach (var existing in currentDefaults)
            {
                existing.IsDefault = false;
                existing.ModifiedUtc = _dateTimeProvider.UtcNow();
            }
        }

        var prompt = new Prompt
        {
            Id = Guid.NewGuid(),
            Name = name,
            Type = PromptType.MatchupPreview,
            Sport = command.Sport,
            WithStats = command.WithStats,
            IsDefault = command.IsDefault,
            Description = command.Description,
            // Normalize line endings so identical text hashes/diffs the same
            // regardless of the editor it was pasted from.
            Text = command.Text.Replace("\r\n", "\n"),
            CreatedUtc = _dateTimeProvider.UtcNow()
        };

        await _dataContext.Prompts.AddAsync(prompt, cancellationToken);

        try
        {
            // Clear-old-default + insert commit in ONE SaveChanges (single
            // transaction); the partial unique indexes on default slots turn
            // a concurrent race into a constraint violation here.
            await _dataContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (command.IsDefault)
        {
            _logger.LogWarning(ex, "Concurrent default-slot change detected while creating prompt {PromptId}.", prompt.Id);
            return new Failure<Guid>(
                default!,
                ResultStatus.BadRequest,
                [new ValidationFailure(nameof(command.IsDefault), "The slot's default changed concurrently — reload and retry.")]);
        }

        _logger.LogInformation(
            "Prompt created. Id: {PromptId}, Name: {Name}, Sport: {Sport}, WithStats: {WithStats}, IsDefault: {IsDefault}, Length: {Length}",
            prompt.Id, LogSanitizer.Clean(prompt.Name), prompt.Sport, prompt.WithStats, prompt.IsDefault, prompt.Text.Length);

        return new Success<Guid>(prompt.Id);
    }
}
