using FluentValidation;
using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

namespace SportsData.Api.Application.Models.Commands.UpdateModel;

/// <summary>
/// Edits a model's METADATA — cutoff (verification updates are the main
/// use), evidence, costs, reasoning cap, release date, IsActive — and its
/// family.
/// Identity fields (Name, ApiModelId, ModelProviderId) are deliberately
/// immutable: a different API identifier is a different model, full stop.
/// Family is grouping, not identity, so it may move. Activating an
/// OpenRouter model (or moving an active one) takes the target family's
/// lab seat, deactivating the previous holder.
/// </summary>
public class UpdateModelCommand
{
    public Guid ModelId { get; set; }

    public Guid ModelFamilyId { get; set; }

    public DateTime? ReleaseDate { get; set; }

    public DateTime? KnowledgeCutoffUtc { get; set; }

    public string? CutoffEvidence { get; set; }

    public DateTime? CutoffVerifiedUtc { get; set; }

    public decimal? InputCostPerMTok { get; set; }

    public decimal? OutputCostPerMTok { get; set; }

    /// <summary>Reasoning cap as an effort level (OpenRouter route only; exclusive with <see cref="ReasoningMaxTokens"/>). Null = model default.</summary>
    public ModelReasoningEffort? ReasoningEffort { get; set; }

    /// <summary>Reasoning cap as a token budget (OpenRouter route only; exclusive with <see cref="ReasoningEffort"/>). Null = model default.</summary>
    public int? ReasoningMaxTokens { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdateModelCommandValidator : AbstractValidator<UpdateModelCommand>
{
    public UpdateModelCommandValidator()
    {
        RuleFor(x => x.ModelId).NotEmpty();
        RuleFor(x => x.ModelFamilyId).NotEmpty();
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
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == command.ModelId, cancellationToken);

        if (model is null)
        {
            return new Failure<Guid>(
                default!,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(command.ModelId), "Model not found")]);
        }

        // Gateway is create-only, so the pre-read's value holds for the attempt.
        var reasoningFailure = ReasoningCap.Validate(model.Gateway, command.ReasoningEffort, command.ReasoningMaxTokens);
        if (reasoningFailure is not null)
        {
            return new Failure<Guid>(default!, ResultStatus.Validation, [reasoningFailure]);
        }

        if (model.ModelFamilyId != command.ModelFamilyId)
        {
            var familyExists = await _dataContext.ModelFamilies
                .AsNoTracking()
                .AnyAsync(f => f.Id == command.ModelFamilyId, cancellationToken);

            if (!familyExists)
            {
                return new Failure<Guid>(
                    default!,
                    ResultStatus.Validation,
                    [new ValidationFailure(nameof(command.ModelFamilyId), "Model family not found")]);
            }
        }

        // Decided per attempt from the model reloaded inside the
        // transaction: a concurrent swap between the read above and the
        // attempt can change whether this update takes the seat.
        var takesSeat = false;

        List<string> displaced;

        try
        {
            // Two saves in ONE transaction: first vacate the family's lab
            // seat, then apply the update. In a single SaveChanges EF orders
            // same-table updates by key, not by the partial unique index it
            // cannot see depends on IsActive, and PostgreSQL checks
            // non-deferrable unique indexes per statement — the incoming
            // activation could land first and collide. A concurrent race
            // still surfaces as a constraint violation here. Execution
            // strategy wrap: EnableRetryOnFailure forbids a raw transaction.
            // Each attempt starts from a clear change tracker and reloads:
            // a retry follows a rolled-back transaction, and entities still
            // tracked from the failed attempt would read as already saved
            // (no dirty updates), so the retry would commit nothing.
            var strategy = _dataContext.Database.CreateExecutionStrategy();
            displaced = await strategy.ExecuteAsync(async () =>
            {
                _dataContext.ChangeTracker.Clear();
                await using var transaction = await _dataContext.Database.BeginTransactionAsync(cancellationToken);

                var current = await _dataContext.Models
                    .FirstAsync(m => m.Id == command.ModelId, cancellationToken);

                takesSeat = LabSeat.Occupies(current.Gateway, command.IsActive)
                    && (!current.IsActive || current.ModelFamilyId != command.ModelFamilyId);

                var vacated = takesSeat
                    ? await LabSeat.VacateAsync(_dataContext, command.ModelFamilyId, model.Id, _dateTimeProvider.UtcNow(), cancellationToken)
                    : new List<string>();

                await _dataContext.SaveChangesAsync(cancellationToken);

                current.ModelFamilyId = command.ModelFamilyId;
                current.ReleaseDate = command.ReleaseDate;
                current.KnowledgeCutoffUtc = command.KnowledgeCutoffUtc;
                current.CutoffEvidence = command.CutoffEvidence;
                current.CutoffVerifiedUtc = command.CutoffVerifiedUtc;
                current.InputCostPerMTok = command.InputCostPerMTok;
                current.OutputCostPerMTok = command.OutputCostPerMTok;
                current.ReasoningEffort = command.ReasoningEffort;
                current.ReasoningMaxTokens = command.ReasoningMaxTokens;
                current.IsActive = command.IsActive;
                current.ModifiedUtc = _dateTimeProvider.UtcNow();

                await _dataContext.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);
                return vacated;
            });
        }
        catch (DbUpdateException ex) when (takesSeat)
        {
            _logger.LogWarning(ex, "Concurrent lab-seat change detected while updating model {ModelId}.", model.Id);
            return new Failure<Guid>(
                default!,
                ResultStatus.BadRequest,
                [new ValidationFailure(nameof(command.ModelFamilyId), "The family's lab seat changed concurrently — reload and retry.")]);
        }

        _logger.LogInformation(
            "Model updated. Id: {ModelId}, Family: {FamilyId}, Cutoff: {Cutoff}, VerifiedUtc: {VerifiedUtc}, ReasoningEffort: {ReasoningEffort}, ReasoningMaxTokens: {ReasoningMaxTokens}, IsActive: {IsActive}, DisplacedFromSeat: {Displaced}",
            model.Id, command.ModelFamilyId, command.KnowledgeCutoffUtc, command.CutoffVerifiedUtc, command.ReasoningEffort, command.ReasoningMaxTokens, command.IsActive, displaced);

        return new Success<Guid>(model.Id);
    }
}
