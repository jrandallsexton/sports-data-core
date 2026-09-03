using FluentValidation;
using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

namespace SportsData.Api.Application.Admin.Models;

public class CreateModelCommand
{
    public Guid ModelProviderId { get; set; }

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

    /// <summary>Make this THE production model; clears the previous default.</summary>
    public bool IsDefault { get; set; }
}

public class CreateModelCommandValidator : AbstractValidator<CreateModelCommand>
{
    public CreateModelCommandValidator()
    {
        RuleFor(x => x.ModelProviderId).NotEmpty();
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
                [new ValidationFailure(nameof(command.ModelProviderId), "Model provider not found — create it first (POST /admin/model-providers)")]);
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

        var model = new Model
        {
            Id = Guid.NewGuid(),
            ModelProviderId = command.ModelProviderId,
            Name = name,
            ApiModelId = apiModelId,
            Gateway = command.Gateway,
            ReleaseDate = command.ReleaseDate,
            KnowledgeCutoffUtc = command.KnowledgeCutoffUtc,
            CutoffEvidence = command.CutoffEvidence,
            CutoffVerifiedUtc = command.CutoffVerifiedUtc,
            InputCostPerMTok = command.InputCostPerMTok,
            OutputCostPerMTok = command.OutputCostPerMTok,
            IsDefault = command.IsDefault,
            CreatedUtc = _dateTimeProvider.UtcNow()
        };

        await _dataContext.Models.AddAsync(model, cancellationToken);

        try
        {
            // Clear-old-default + insert commit in ONE SaveChanges; the
            // partial unique index turns a concurrent race into a
            // constraint violation here.
            await _dataContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (command.IsDefault)
        {
            _logger.LogWarning(ex, "Concurrent default-model change detected while creating model {ModelId}.", model.Id);
            return new Failure<Guid>(
                default!,
                ResultStatus.BadRequest,
                [new ValidationFailure(nameof(command.IsDefault), "The default model changed concurrently — reload and retry.")]);
        }

        _logger.LogInformation(
            "Model created. Id: {ModelId}, Provider: {ProviderId}, IsDefault: {IsDefault}, Cutoff: {Cutoff}",
            model.Id, model.ModelProviderId, model.IsDefault, model.KnowledgeCutoffUtc);

        return new Success<Guid>(model.Id);
    }
}
