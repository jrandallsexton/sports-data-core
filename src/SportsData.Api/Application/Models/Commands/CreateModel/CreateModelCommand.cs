using FluentValidation;
using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

namespace SportsData.Api.Application.Models.Commands.CreateModel;

public class CreateModelCommand
{
    public Guid ModelProviderId { get; set; }

    /// <summary>The lab seat lineage (e.g. "Qwen"). An OpenRouter model takes the family's seat, deactivating the previous holder.</summary>
    public Guid ModelFamilyId { get; set; }

    /// <summary>Display name (unique), e.g. "Claude Haiku 4.5".</summary>
    public required string Name { get; set; }

    /// <summary>Exact API identifier for the chosen route, e.g. "claude-haiku-4-5" direct, "anthropic/claude-sonnet-4.5" via OpenRouter.</summary>
    public required string ApiModelId { get; set; }

    /// <summary>How the model is reached (None = first-party client). Identity, like ApiModelId — a different route is a different evaluand, so it is create-only.</summary>
    public ModelGateway Gateway { get; set; } = ModelGateway.None;

    public DateTime? ReleaseDate { get; set; }

    /// <summary>Declared TRAINING cutoff; null = provider does not publish (treated higher-risk).</summary>
    public DateTime? KnowledgeCutoffUtc { get; set; }

    public string? CutoffEvidence { get; set; }

    public DateTime? CutoffVerifiedUtc { get; set; }

    public decimal? InputCostPerMTok { get; set; }

    public decimal? OutputCostPerMTok { get; set; }

    /// <summary>Reasoning cap as an effort level (OpenRouter route only; exclusive with <see cref="ReasoningMaxTokens"/>). Null = model default.</summary>
    public ModelReasoningEffort? ReasoningEffort { get; set; }

    /// <summary>Reasoning cap as a token budget (OpenRouter route only; exclusive with <see cref="ReasoningEffort"/>). Null = model default.</summary>
    public int? ReasoningMaxTokens { get; set; }

    /// <summary>Make this THE production model; clears the previous default.</summary>
    public bool IsDefault { get; set; }
}

public class CreateModelCommandValidator : AbstractValidator<CreateModelCommand>
{
    public CreateModelCommandValidator()
    {
        RuleFor(x => x.ModelProviderId).NotEmpty();
        RuleFor(x => x.ModelFamilyId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ApiModelId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Gateway).IsInEnum();
        RuleFor(x => x.CutoffEvidence).MaximumLength(512);
    }
}

public interface ICreateModelCommandHandler
{
    Task<Result<Guid>> ExecuteAsync(CreateModelCommand command, CancellationToken cancellationToken);
}

public class CreateModelCommandHandler : ICreateModelCommandHandler
{
    private readonly AppDataContext _dataContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<CreateModelCommandHandler> _logger;

    public CreateModelCommandHandler(
        AppDataContext dataContext,
        IDateTimeProvider dateTimeProvider,
        ILogger<CreateModelCommandHandler> logger)
    {
        _dataContext = dataContext;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<Result<Guid>> ExecuteAsync(CreateModelCommand command, CancellationToken cancellationToken)
    {
        var validation = await new CreateModelCommandValidator().ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return new Failure<Guid>(default!, ResultStatus.Validation, validation.Errors);
        }

        var provider = await _dataContext.ModelProviders
            .AsNoTracking()
            .Where(p => p.Id == command.ModelProviderId)
            .Select(p => new { p.Kind })
            .FirstOrDefaultAsync(cancellationToken);

        if (provider is null)
        {
            return new Failure<Guid>(
                default!,
                ResultStatus.Validation,
                [new ValidationFailure(nameof(command.ModelProviderId), "Model provider not found — create it first (POST /api/model-providers)")]);
        }

        // A GatewayOnly provider has no first-party client by definition —
        // a direct-routed model under it would be unreachable forever.
        if (provider.Kind == ModelProviderKind.GatewayOnly && command.Gateway == ModelGateway.None)
        {
            return new Failure<Guid>(
                default!,
                ResultStatus.Validation,
                [new ValidationFailure(nameof(command.Gateway), "This provider is gateway-only (no first-party client) — the model must specify a gateway route.")]);
        }

        var reasoningFailure = ReasoningCap.Validate(command.Gateway, command.ReasoningEffort, command.ReasoningMaxTokens);
        if (reasoningFailure is not null)
        {
            return new Failure<Guid>(default!, ResultStatus.Validation, [reasoningFailure]);
        }

        var familyExists = await _dataContext.ModelFamilies
            .AsNoTracking()
            .AnyAsync(f => f.Id == command.ModelFamilyId, cancellationToken);

        if (!familyExists)
        {
            return new Failure<Guid>(
                default!,
                ResultStatus.Validation,
                [new ValidationFailure(nameof(command.ModelFamilyId), "Model family not found — create it first (POST /api/model-families)")]);
        }

        var name = command.Name.Trim();
        var apiModelId = command.ApiModelId.Trim();

        var duplicate = await _dataContext.Models
            .AsNoTracking()
            .AnyAsync(m => m.Name == name
                        || (m.ModelProviderId == command.ModelProviderId && m.ApiModelId == apiModelId),
                cancellationToken);

        if (duplicate)
        {
            return new Failure<Guid>(
                default!,
                ResultStatus.Validation,
                [new ValidationFailure(nameof(command.Name), $"A model with this name or (provider, api id) already exists.")]);
        }

        var modelId = Guid.NewGuid();
        var takesSeat = LabSeat.Occupies(command.Gateway, isActive: true);

        var model = new Model
        {
            Id = modelId,
            ModelProviderId = command.ModelProviderId,
            ModelFamilyId = command.ModelFamilyId,
            Name = name,
            ApiModelId = apiModelId,
            Gateway = command.Gateway,
            ReleaseDate = command.ReleaseDate,
            KnowledgeCutoffUtc = command.KnowledgeCutoffUtc,
            CutoffEvidence = command.CutoffEvidence,
            CutoffVerifiedUtc = command.CutoffVerifiedUtc,
            InputCostPerMTok = command.InputCostPerMTok,
            OutputCostPerMTok = command.OutputCostPerMTok,
            ReasoningEffort = command.ReasoningEffort,
            ReasoningMaxTokens = command.ReasoningMaxTokens,
            IsDefault = command.IsDefault,
            CreatedUtc = _dateTimeProvider.UtcNow()
        };

        List<string> displaced;

        try
        {
            // Two saves in ONE transaction: first vacate the default and the
            // family's lab seat, then insert. In a single SaveChanges EF may
            // send the insert first (it does not see that the partial unique
            // indexes depend on IsDefault / IsActive), and PostgreSQL checks
            // non-deferrable unique indexes per statement. A concurrent race
            // still surfaces as a constraint violation here. Execution
            // strategy wrap: EnableRetryOnFailure forbids a raw transaction.
            // Each attempt starts from a clear change tracker: a retry
            // follows a rolled-back transaction, and entities still tracked
            // from the failed attempt would read as already saved, so the
            // vacate would issue no updates and the insert would collide.
            var strategy = _dataContext.Database.CreateExecutionStrategy();
            displaced = await strategy.ExecuteAsync(async () =>
            {
                _dataContext.ChangeTracker.Clear();
                await using var transaction = await _dataContext.Database.BeginTransactionAsync(cancellationToken);

                if (command.IsDefault)
                {
                    var currentDefaults = await _dataContext.Models
                        .Where(m => m.IsDefault)
                        .ToListAsync(cancellationToken);

                    foreach (var existing in currentDefaults)
                    {
                        existing.IsDefault = false;
                        existing.ModifiedUtc = _dateTimeProvider.UtcNow();
                    }
                }

                // New rows are active, so an OpenRouter model takes its family's seat.
                var vacated = takesSeat
                    ? await LabSeat.VacateAsync(_dataContext, command.ModelFamilyId, modelId, _dateTimeProvider.UtcNow(), cancellationToken)
                    : new List<string>();

                await _dataContext.SaveChangesAsync(cancellationToken);

                await _dataContext.Models.AddAsync(model, cancellationToken);
                await _dataContext.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);
                return vacated;
            });
        }
        catch (DbUpdateException ex) when (command.IsDefault || takesSeat)
        {
            _logger.LogWarning(ex, "Concurrent default-model or lab-seat change detected while creating model {ModelId}.", model.Id);
            return new Failure<Guid>(
                default!,
                ResultStatus.BadRequest,
                [new ValidationFailure(nameof(command.ModelFamilyId), "The default model or the family's lab seat changed concurrently — reload and retry.")]);
        }

        _logger.LogInformation(
            "Model created. Id: {ModelId}, Provider: {ProviderId}, Family: {FamilyId}, IsDefault: {IsDefault}, Cutoff: {Cutoff}, ReasoningEffort: {ReasoningEffort}, ReasoningMaxTokens: {ReasoningMaxTokens}, DisplacedFromSeat: {Displaced}",
            model.Id, model.ModelProviderId, model.ModelFamilyId, model.IsDefault, model.KnowledgeCutoffUtc, model.ReasoningEffort, model.ReasoningMaxTokens, displaced);

        return new Success<Guid>(model.Id);
    }
}
