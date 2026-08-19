#nullable enable

using FluentValidation.Results;

using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.Notification.Dtos;
using SportsData.Notification.Infrastructure.Data;
using SportsData.Notification.Infrastructure.Data.Entities;

namespace SportsData.Notification.Application.Smack.Commands.CreateSmackPhrase;

public interface ICreateSmackPhraseCommandHandler
{
    Task<Result<SmackPhraseDto>> ExecuteAsync(
        SmackPhraseUpsertDto request,
        CancellationToken cancellationToken = default);
}

public class CreateSmackPhraseCommandHandler : ICreateSmackPhraseCommandHandler
{
    private readonly ILogger<CreateSmackPhraseCommandHandler> _logger;
    private readonly AppDataContext _dataContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    public CreateSmackPhraseCommandHandler(
        ILogger<CreateSmackPhraseCommandHandler> logger,
        AppDataContext dataContext,
        IDateTimeProvider dateTimeProvider)
    {
        _logger = logger;
        _dataContext = dataContext;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<SmackPhraseDto>> ExecuteAsync(
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

        var entity = new SmackPhrase
        {
            Id = Guid.NewGuid(),
            Voice = voice,
            Situation = situation,
            Sport = sport,
            Text = request.Text!.Trim(),
            IsActive = request.IsActive,
            RequiresGamblingContent = request.RequiresGamblingContent,
            Weight = request.Weight,
            Description = request.Description,
            CreatedUtc = _dateTimeProvider.UtcNow(),
            CreatedBy = Guid.Empty // operator-authored via admin key; no user identity on this path
        };

        _dataContext.SmackPhrases.Add(entity);
        await _dataContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "SmackPhrase created. PhraseId={PhraseId}, Situation={Situation}, Voice={Voice}",
            entity.Id, entity.Situation, entity.Voice);

        return new Success<SmackPhraseDto>(SmackPhraseUpsertValidation.ToDto(entity));
    }
}
