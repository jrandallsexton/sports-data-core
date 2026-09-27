using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FluentValidation;
using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.DependencyInjection;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.FranchiseSeasons.Commands.RequestSingleFranchiseSeasonSourcing;

public interface IRequestSingleFranchiseSeasonSourcingCommandHandler
{
    Task<Result<Guid>> ExecuteAsync(
        RequestSingleFranchiseSeasonSourcingCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Publishes one DocumentRequested for the franchise season's ESPN TeamSeason
/// document — the per-team body of
/// <see cref="RequestFranchiseSeasonSourcing.RequestFranchiseSeasonSourcingCommandHandler"/>,
/// same message shape, for one team. Provider fetches the document and
/// TeamSeasonDocumentProcessor re-runs it as an update.
///
/// IncludeLinkedDocumentTypes is null on purpose: null means "spawn
/// everything", so the full child tree cascades — events (the schedule),
/// records, stats, roster. The filter propagates to children, so narrowing it
/// to Event would stop each event's own children (competitions) from being
/// sourced.
///
/// Cost: for the CURRENT season the Provider neither serves its Mongo cache
/// nor applies its republish suppression (both are gated on
/// IsCurrentSeason), so every call re-fetches the whole tree from ESPN live.
/// That is the point for a current-season repair, but the action is not
/// free to repeat. Only historical seasons get the suppression.
///
/// Unlike the bulk handler, a franchise season with no usable ESPN ref is a
/// failure rather than a skip: sourcing is this action's only job.
/// </summary>
public class RequestSingleFranchiseSeasonSourcingCommandHandler : IRequestSingleFranchiseSeasonSourcingCommandHandler
{
    private readonly ILogger<RequestSingleFranchiseSeasonSourcingCommandHandler> _logger;
    private readonly TeamSportDataContext _dataContext;
    private readonly IAppMode _appMode;
    private readonly IEventBus _eventBus;
    private readonly IMessageDeliveryScope _deliveryScope;
    private readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;
    private readonly IValidator<RequestSingleFranchiseSeasonSourcingCommand> _validator;

    public RequestSingleFranchiseSeasonSourcingCommandHandler(
        ILogger<RequestSingleFranchiseSeasonSourcingCommandHandler> logger,
        TeamSportDataContext dataContext,
        IAppMode appMode,
        IEventBus eventBus,
        IMessageDeliveryScope deliveryScope,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IValidator<RequestSingleFranchiseSeasonSourcingCommand> validator)
    {
        _logger = logger;
        _dataContext = dataContext;
        _appMode = appMode;
        _eventBus = eventBus;
        _deliveryScope = deliveryScope;
        _externalRefIdentityGenerator = externalRefIdentityGenerator;
        _validator = validator;
    }

    public async Task<Result<Guid>> ExecuteAsync(
        RequestSingleFranchiseSeasonSourcingCommand command,
        CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return new Failure<Guid>(default, ResultStatus.Validation, validation.Errors);
        }

        var franchiseSeason = await _dataContext.FranchiseSeasons
            .AsNoTracking()
            .Where(x => x.Id == command.FranchiseSeasonId)
            .Select(x => new { x.Id, x.SeasonYear })
            .FirstOrDefaultAsync(cancellationToken);

        if (franchiseSeason is null)
        {
            return new Failure<Guid>(
                default,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(command.FranchiseSeasonId),
                    $"FranchiseSeason {command.FranchiseSeasonId} not found.")]);
        }

        // A separate top-level query, not a scalar subquery in the projection
        // above: "no ESPN external id" is exactly the case the guard below
        // exists for, and a top-level FirstOrDefault returns null for zero
        // rows without depending on EF's nullability inference over a
        // non-nullable column (which the InMemory test provider cannot
        // exercise). Vortex, PR #795.
        var espnSourceUrl = await _dataContext.FranchiseSeasonExternalIds
            .AsNoTracking()
            .Where(e => e.FranchiseSeasonId == franchiseSeason.Id && e.Provider == SourceDataProvider.Espn)
            .Select(e => e.SourceUrl)
            .FirstOrDefaultAsync(cancellationToken);

        var correlationId = command.CorrelationId;

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["FranchiseSeasonId"] = franchiseSeason.Id,
            ["SeasonYear"] = franchiseSeason.SeasonYear,
            ["Sport"] = _appMode.CurrentSport
        }))
        {
            if (string.IsNullOrWhiteSpace(espnSourceUrl) ||
                !Uri.TryCreate(espnSourceUrl, UriKind.Absolute, out var sourceUrl))
            {
                _logger.LogWarning(
                    "FranchiseSeason {FranchiseSeasonId} has no usable ESPN TeamSeason ref; sourcing not requested.",
                    franchiseSeason.Id);
                return new Failure<Guid>(
                    default,
                    ResultStatus.Validation,
                    [new ValidationFailure(nameof(command.FranchiseSeasonId),
                        $"FranchiseSeason {franchiseSeason.Id} has no usable ESPN TeamSeason ref to source from.")]);
            }

            var identity = _externalRefIdentityGenerator.Generate(sourceUrl);

            var request = new DocumentRequested(
                Id: identity.UrlHash,
                ParentId: franchiseSeason.Id.ToString(),
                Uri: new Uri(identity.CleanUrl),
                Ref: null,
                Sport: _appMode.CurrentSport,
                SeasonYear: franchiseSeason.SeasonYear,
                DocumentType: DocumentType.TeamSeason,
                SourceDataProvider: SourceDataProvider.Espn,
                CorrelationId: correlationId,
                CausationId: CausationId.Producer.FranchiseSeasonService,
                IncludeLinkedDocumentTypes: null);

            try
            {
                // Direct, not the EF outbox: this handler never saves, so an
                // outbox publish would be captured and silently dropped.
                // PublishBatch even for one message: the Direct-mode Publish
                // pays a 1-second delay per call; the batch path does not.
                using (_deliveryScope.Use(DeliveryMode.Direct))
                {
                    await _eventBus.PublishBatch([request]);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish sourcing request for FranchiseSeason {FranchiseSeasonId}", franchiseSeason.Id);
                return new Failure<Guid>(
                    default,
                    ResultStatus.Error,
                    [new ValidationFailure("Sourcing",
                        $"Sourcing request could not be published. CorrelationId={correlationId}. See Seq for details.")]);
            }

            _logger.LogInformation(
                "Single franchise season sourcing requested (TeamSeason, full cascade). Uri={Uri}",
                identity.CleanUrl);

            return new Success<Guid>(correlationId, ResultStatus.Accepted);
        }
    }
}
