using FluentValidation;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.FranchiseSeasons.Commands.RequestFranchiseSeasonSourcing;

public interface IRequestFranchiseSeasonSourcingCommandHandler
{
    Task<Result<Guid>> ExecuteAsync(
        RequestFranchiseSeasonSourcingCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Fans out a DocumentRequested per FranchiseSeason for a season year — the
/// bulk-sourcing entry point for a season's team data (immediate driver: the
/// NFL 2026 schedule). Provider fetches each TeamSeason document from ESPN and
/// publishes DocumentCreated; TeamSeasonDocumentProcessor takes it from there.
///
/// IncludeLinkedDocumentTypes is DELIBERATELY omitted: null means "spawn
/// everything" (see DocumentProcessorBase.ShouldSpawn), so the full document
/// tree — events/schedule included — cascades from each team season. The
/// filter also PROPAGATES to children, so narrowing it here (e.g. to Event
/// only) would strangle the cascade below the first hop, not just limit it.
/// </summary>
public class RequestFranchiseSeasonSourcingCommandHandler : IRequestFranchiseSeasonSourcingCommandHandler
{
    private readonly ILogger<RequestFranchiseSeasonSourcingCommandHandler> _logger;
    private readonly TeamSportDataContext _dataContext;
    private readonly IEventBus _eventBus;
    private readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;
    private readonly IValidator<RequestFranchiseSeasonSourcingCommand> _validator;
    private readonly IMessageDeliveryScope _deliveryScope;

    public RequestFranchiseSeasonSourcingCommandHandler(
        ILogger<RequestFranchiseSeasonSourcingCommandHandler> logger,
        TeamSportDataContext dataContext,
        IEventBus eventBus,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IValidator<RequestFranchiseSeasonSourcingCommand> validator,
        IMessageDeliveryScope deliveryScope)
    {
        _logger = logger;
        _dataContext = dataContext;
        _eventBus = eventBus;
        _externalRefIdentityGenerator = externalRefIdentityGenerator;
        _validator = validator;
        _deliveryScope = deliveryScope;
    }

    public async Task<Result<Guid>> ExecuteAsync(
        RequestFranchiseSeasonSourcingCommand command,
        CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return new Failure<Guid>(default!, ResultStatus.Validation, validation.Errors);

        var franchiseSeasons = await _dataContext.FranchiseSeasons
            .AsNoTracking()
            .Include(fs => fs.Franchise)
            .Include(fs => fs.ExternalIds)
            .Where(fs =>
                fs.SeasonYear == command.SeasonYear &&
                fs.Franchise.Sport == command.Sport)
            .ToListAsync(cancellationToken);

        if (franchiseSeasons.Count == 0)
        {
            _logger.LogWarning(
                "No franchise seasons found to source. SeasonYear={SeasonYear}, Sport={Sport}",
                command.SeasonYear, command.Sport);
            return new Success<Guid>(Guid.Empty, ResultStatus.NotFound);
        }

        var correlationId = Guid.NewGuid();
        var requested = 0;
        var skipped = 0;
        var failed = 0;

        // Direct delivery, NOT the bus-outbox: this handler only READS
        // (AsNoTracking) and writes no entity, so SaveChangesAsync would have
        // nothing to persist and would never flush the outbox — the events
        // would be enqueued into the tracker and silently dropped. Publish
        // straight to the broker instead. See
        // feedback_direct_publish_no_dbcontext / AdminController's
        // BroadcastDebugContestStatus.
        using (_deliveryScope.Use(DeliveryMode.Direct))
        {
            foreach (var fs in franchiseSeasons)
            {
                var externalId = fs.ExternalIds
                    .FirstOrDefault(x => x.Provider == SourceDataProvider.Espn);

                if (externalId is null || string.IsNullOrWhiteSpace(externalId.SourceUrl) ||
                    !Uri.TryCreate(externalId.SourceUrl, UriKind.Absolute, out var sourceUrl))
                {
                    // Log-and-continue: one franchise's missing/garbage ref must
                    // not stop the other 31. The count surfaces in the summary.
                    _logger.LogWarning(
                        "Skipping franchise season {FranchiseSeasonId}: no usable ESPN SourceUrl. SeasonYear={SeasonYear}",
                        fs.Id, command.SeasonYear);
                    skipped++;
                    continue;
                }

                var identity = _externalRefIdentityGenerator.Generate(sourceUrl);

                try
                {
                    await _eventBus.Publish(new DocumentRequested(
                        Id: identity.UrlHash,
                        ParentId: fs.Id.ToString(),
                        Uri: new Uri(identity.CleanUrl),
                        Ref: null,
                        Sport: command.Sport,
                        SeasonYear: command.SeasonYear,
                        DocumentType: DocumentType.TeamSeason,
                        SourceDataProvider: SourceDataProvider.Espn,
                        CorrelationId: correlationId,
                        CausationId: CausationId.Producer.FranchiseSeasonService
                    ), cancellationToken);

                    requested++;
                }
                catch (Exception ex)
                {
                    // Same log-and-continue contract as the missing-ref skip: a
                    // broker hiccup on one franchise must not abandon the rest
                    // of the batch (and the correlation id). Under at-least-once
                    // this is safe to re-request — a later re-run publishes the
                    // same idempotent DocumentRequested for the failed ones.
                    _logger.LogError(ex,
                        "Failed to publish sourcing request for franchise season {FranchiseSeasonId}. SeasonYear={SeasonYear}",
                        fs.Id, command.SeasonYear);
                    failed++;
                }
            }
        }

        _logger.LogInformation(
            "FranchiseSeason sourcing requested. SeasonYear={SeasonYear}, Sport={Sport}, Requested={Requested}, Skipped={Skipped}, Failed={Failed}, CorrelationId={CorrelationId}",
            command.SeasonYear, command.Sport, requested, skipped, failed, correlationId);

        return new Success<Guid>(correlationId, ResultStatus.Accepted);
    }
}
