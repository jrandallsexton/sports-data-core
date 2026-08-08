using FluentValidation;
using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;

namespace SportsData.Api.Application.Admin.Models;

/// <summary>
/// Edits a model's METADATA — cutoff (verification updates are the main
/// use), evidence, costs, release date, IsActive. Identity fields
/// (Name, ApiModelId, ModelProviderId) are deliberately immutable: a
/// different API identifier is a different model, full stop.
/// </summary>
public class UpdateModelCommand
{
    public Guid ModelId { get; set; }

    public DateTime? ReleaseDate { get; set; }

    public DateTime? KnowledgeCutoffUtc { get; set; }

    public string? CutoffEvidence { get; set; }

    public DateTime? CutoffVerifiedUtc { get; set; }

    public decimal? InputCostPerMTok { get; set; }

    public decimal? OutputCostPerMTok { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdateModelCommandValidator : AbstractValidator<UpdateModelCommand>
{
    public UpdateModelCommandValidator()
    {
        RuleFor(x => x.ModelId).NotEmpty();
        RuleFor(x => x.CutoffEvidence).MaximumLength(512);
    }
}

public interface IUpdateModelCommandHandler
{
    Task<Result<Guid>> ExecuteAsync(UpdateModelCommand command, CancellationToken cancellationToken);
}

public class UpdateModelCommandHandler : IUpdateModelCommandHandler
{
    private readonly AppDataContext _dataContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<UpdateModelCommandHandler> _logger;

    public UpdateModelCommandHandler(
        AppDataContext dataContext,
        IDateTimeProvider dateTimeProvider,
        ILogger<UpdateModelCommandHandler> logger)
    {
        _dataContext = dataContext;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<Result<Guid>> ExecuteAsync(UpdateModelCommand command, CancellationToken cancellationToken)
    {
        var validation = await new UpdateModelCommandValidator().ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return new Failure<Guid>(default!, ResultStatus.Validation, validation.Errors);
        }

        var model = await _dataContext.Models
            .FirstOrDefaultAsync(m => m.Id == command.ModelId, cancellationToken);

        if (model is null)
        {
            return new Failure<Guid>(
                default!,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(command.ModelId), "Model not found")]);
        }

        model.ReleaseDate = command.ReleaseDate;
        model.KnowledgeCutoffUtc = command.KnowledgeCutoffUtc;
        model.CutoffEvidence = command.CutoffEvidence;
        model.CutoffVerifiedUtc = command.CutoffVerifiedUtc;
        model.InputCostPerMTok = command.InputCostPerMTok;
        model.OutputCostPerMTok = command.OutputCostPerMTok;
        model.IsActive = command.IsActive;
        model.ModifiedUtc = _dateTimeProvider.UtcNow();

        await _dataContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Model updated. Id: {ModelId}, Cutoff: {Cutoff}, VerifiedUtc: {VerifiedUtc}, IsActive: {IsActive}",
            model.Id, model.KnowledgeCutoffUtc, model.CutoffVerifiedUtc, model.IsActive);

        return new Success<Guid>(model.Id);
    }
}

public interface ISetDefaultModelCommandHandler
{
    Task<Result<Guid>> ExecuteAsync(Guid modelId, CancellationToken cancellationToken);
}

/// <summary>
/// Makes a model THE production default (single global slot), clearing
/// the previous default. Effective on the next generation run — the
/// pre-season model selection, and any in-season swap, is this call.
/// </summary>
public class SetDefaultModelCommandHandler : ISetDefaultModelCommandHandler
{
    private readonly AppDataContext _dataContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<SetDefaultModelCommandHandler> _logger;

    public SetDefaultModelCommandHandler(
        AppDataContext dataContext,
        IDateTimeProvider dateTimeProvider,
        ILogger<SetDefaultModelCommandHandler> logger)
    {
        _dataContext = dataContext;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<Result<Guid>> ExecuteAsync(Guid modelId, CancellationToken cancellationToken)
    {
        var model = await _dataContext.Models
            .FirstOrDefaultAsync(m => m.Id == modelId, cancellationToken);

        if (model is null)
        {
            return new Failure<Guid>(
                default!,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(modelId), "Model not found")]);
        }

        if (!model.IsActive)
        {
            return new Failure<Guid>(
                default!,
                ResultStatus.Validation,
                [new ValidationFailure(nameof(modelId), "An inactive model cannot be the production default")]);
        }

        if (!model.IsDefault)
        {
            var currentDefaults = await _dataContext.Models
                .Where(m => m.Id != model.Id && m.IsDefault)
                .ToListAsync(cancellationToken);

            foreach (var existing in currentDefaults)
            {
                existing.IsDefault = false;
                existing.ModifiedUtc = _dateTimeProvider.UtcNow();
            }

            model.IsDefault = true;
            model.ModifiedUtc = _dateTimeProvider.UtcNow();

            try
            {
                await _dataContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogWarning(ex, "Concurrent default-model change detected while promoting model {ModelId}.", model.Id);
                return new Failure<Guid>(
                    default!,
                    ResultStatus.BadRequest,
                    [new ValidationFailure(nameof(modelId), "The default model changed concurrently — reload and retry.")]);
            }

            _logger.LogInformation(
                "Model {ModelId} ('{Name}') is now the production default; {Cleared} previous default(s) cleared.",
                model.Id, model.Name, currentDefaults.Count);
        }

        return new Success<Guid>(model.Id);
    }
}
