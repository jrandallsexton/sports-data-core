#nullable enable

using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.Notification.Dtos;
using SportsData.Notification.Infrastructure.Data;

namespace SportsData.Notification.Application.Smack.Commands.UpdateSmackPhrase;

public interface IUpdateSmackPhraseCommandHandler
{
    Task<Result<SmackPhraseDto>> ExecuteAsync(
        Guid id,
        SmackPhraseUpsertDto request,
        CancellationToken cancellationToken = default);
}

public class UpdateSmackPhraseCommandHandler : IUpdateSmackPhraseCommandHandler
{
    private readonly ILogger<UpdateSmackPhraseCommandHandler> _logger;
    private readonly AppDataContext _dataContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    public UpdateSmackPhraseCommandHandler(
        ILogger<UpdateSmackPhraseCommandHandler> logger,
        AppDataContext dataContext,
        IDateTimeProvider dateTimeProvider)
    {
        _logger = logger;
        _dataContext = dataContext;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<SmackPhraseDto>> ExecuteAsync(
        Guid id,
        SmackPhraseUpsertDto request,
        CancellationToken cancellationToken = default)
    {
        var validationError = SmackPhraseUpsertValidation.Validate(
            request, out var voice, out var situation, out var sport);
        if (validationError is not null)
        {
            return new Failure<SmackPhraseDto>(
                default!, ResultStatus.BadRequest,
                [new ValidationFailure(nameof(request), validationError)]);
        }

        if (request.RowVersion is null)
        {
            return new Failure<SmackPhraseDto>(
                default!, ResultStatus.BadRequest,
                [new ValidationFailure(nameof(request.RowVersion),
                    "RowVersion is required on update - send back the value from GET.")]);
        }

        var entity = await _dataContext.SmackPhrases
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (entity is null)
        {
            return new Failure<SmackPhraseDto>(
                default!, ResultStatus.NotFound,
                [new ValidationFailure(nameof(id), $"Phrase {id} not found.")]);
        }

        // Optimistic concurrency on xmin: the client echoes the version it
        // edited from; a stale editor gets Conflict instead of silently
        // clobbering a newer edit.
        _dataContext.Entry(entity).Property(p => p.RowVersion).OriginalValue = request.RowVersion.Value;

        entity.Voice = voice;
        entity.Situation = situation;
        entity.Sport = sport;
        entity.Text = request.Text!.Trim();
        entity.IsActive = request.IsActive;
        entity.RequiresGamblingContent = request.RequiresGamblingContent;
        entity.Weight = request.Weight;
        entity.Description = request.Description;
        entity.ModifiedUtc = _dateTimeProvider.UtcNow();

        try
        {
            await _dataContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new Failure<SmackPhraseDto>(
                default!, ResultStatus.Conflict,
                [new ValidationFailure(nameof(request.RowVersion),
                    "The phrase was modified by someone else. Reload and re-apply your edit.")]);
        }

        _logger.LogInformation("SmackPhrase updated. PhraseId={PhraseId}", id);

        return new Success<SmackPhraseDto>(SmackPhraseUpsertValidation.ToDto(entity));
    }
}
