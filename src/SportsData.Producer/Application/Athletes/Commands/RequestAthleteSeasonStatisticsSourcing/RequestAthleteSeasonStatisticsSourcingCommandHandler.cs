using FluentValidation;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Athletes.Commands.RequestAthleteSeasonStatisticsSourcing;

public interface IRequestAthleteSeasonStatisticsSourcingCommandHandler
{
    Task<Result<Guid>> ExecuteAsync(
        RequestAthleteSeasonStatisticsSourcingCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Fans out a DocumentRequested per ACTIVE AthleteSeason for a season year,
/// targeting an explicitly season-type-scoped statistics URL synthesized
/// from the roster row's own ESPN ref (immediate driver: the 2025 NCAAFB
/// season-stats backfill). The URL must be synthesized rather than taken
/// from ESPN's athlete document because that document hands out the PRIOR
/// season's statistics ref until the new season has data — the root cause
/// of the mislabeled-2026-stats mess this backfill repairs. The
/// AthleteSeasonStatisticsDocumentProcessor's season guard then attaches
/// each document to the athlete's roster row for the season embedded in
/// the ref, so requests are safe to re-run and safe even if a target row
/// is missing (logged skip).
/// </summary>
public class RequestAthleteSeasonStatisticsSourcingCommandHandler : IRequestAthleteSeasonStatisticsSourcingCommandHandler
{
    private readonly ILogger<RequestAthleteSeasonStatisticsSourcingCommandHandler> _logger;
    private readonly TeamSportDataContext _dataContext;
    private readonly IEventBus _eventBus;
    private readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;
    private readonly IValidator<RequestAthleteSeasonStatisticsSourcingCommand> _validator;
    private readonly IMessageDeliveryScope _deliveryScope;

    public RequestAthleteSeasonStatisticsSourcingCommandHandler(
        ILogger<RequestAthleteSeasonStatisticsSourcingCommandHandler> logger,
        TeamSportDataContext dataContext,
        IEventBus eventBus,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IValidator<RequestAthleteSeasonStatisticsSourcingCommand> validator,
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
        RequestAthleteSeasonStatisticsSourcingCommand command,
        CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return new Failure<Guid>(default!, ResultStatus.Validation, validation.Errors);

        // Active roster rows for the season, with their season-scoped ESPN
        // athlete ref. Projected, not Include'd — this is the only shape the
        // fan-out needs, and at ~25k rows per season the narrow select
        // matters.
        var targets = await _dataContext.AthleteSeasons
            .AsNoTracking()
            .Where(a =>
                a.IsActive &&
                a.FranchiseSeasonId != null &&
                _dataContext.FranchiseSeasons.Any(fs =>
                    fs.Id == a.FranchiseSeasonId &&
                    fs.SeasonYear == command.SeasonYear))
            .Select(a => new
            {
                a.Id,
                SourceUrl = a.ExternalIds
                    .Where(x => x.Provider == SourceDataProvider.Espn)
                    .Select(x => x.SourceUrl)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        if (targets.Count == 0)
        {
            _logger.LogWarning(
                "No active athlete seasons found to source statistics for. SeasonYear={SeasonYear}, Sport={Sport}",
                command.SeasonYear, command.Sport);
            return new Success<Guid>(Guid.Empty, ResultStatus.NotFound);
        }

        // Caller-minted correlation id, same contract as the franchise
        // sourcing endpoint: the 202 response carries the Seq handle for the
        // whole batch before this background job runs.
        var correlationId = command.CorrelationId ?? Guid.NewGuid();
        var requested = 0;
        var skipped = 0;
        var failed = 0;

        // Direct delivery, NOT the bus-outbox: this handler only READS and
        // writes no entity, so SaveChangesAsync would never flush the outbox
        // and the events would be silently dropped. See
        // RequestFranchiseSeasonSourcingCommandHandler.
        using (_deliveryScope.Use(DeliveryMode.Direct))
        {
            foreach (var target in targets)
            {
                Uri statisticsRef;
                try
                {
                    if (string.IsNullOrWhiteSpace(target.SourceUrl) ||
                        !Uri.TryCreate(target.SourceUrl, UriKind.Absolute, out var athleteSeasonRef))
                        throw new InvalidOperationException("No usable ESPN SourceUrl");

                    statisticsRef = EspnUriMapper.AthleteSeasonRefToSeasonTypeStatisticsRef(
                        athleteSeasonRef, command.SeasonType);
                }
                catch (Exception ex)
                {
                    // Log-and-continue: one athlete's missing/garbage ref must
                    // not stop the rest. The count surfaces in the summary.
                    _logger.LogWarning(
                        "Skipping athlete season {AthleteSeasonId}: {Reason}. SeasonYear={SeasonYear}",
                        target.Id, ex.Message, command.SeasonYear);
                    skipped++;
                    continue;
                }

                var identity = _externalRefIdentityGenerator.Generate(statisticsRef);

                try
                {
                    await _eventBus.Publish(new DocumentRequested(
                        Id: identity.UrlHash,
                        ParentId: target.Id.ToString(),
                        Uri: new Uri(identity.CleanUrl),
                        Ref: null,
                        Sport: command.Sport,
                        SeasonYear: command.SeasonYear,
                        DocumentType: DocumentType.AthleteSeasonStatistics,
                        SourceDataProvider: SourceDataProvider.Espn,
                        CorrelationId: correlationId,
                        CausationId: CausationId.Producer.AthleteSeasonStatisticsSourcing,
                        IncludeLinkedDocumentTypes: null
                    ), cancellationToken);

                    requested++;
                }
                catch (Exception ex)
                {
                    // Same log-and-continue contract: under at-least-once a
                    // re-run publishes the same idempotent DocumentRequested
                    // for the failed ones.
                    _logger.LogError(ex,
                        "Failed to publish statistics sourcing request for athlete season {AthleteSeasonId}. SeasonYear={SeasonYear}",
                        target.Id, command.SeasonYear);
                    failed++;
                }
            }
        }

        _logger.LogInformation(
            "AthleteSeason statistics sourcing requested. SeasonYear={SeasonYear}, SeasonType={SeasonType}, Sport={Sport}, Requested={Requested}, Skipped={Skipped}, Failed={Failed}, CorrelationId={CorrelationId}",
            command.SeasonYear, command.SeasonType, command.Sport, requested, skipped, failed, correlationId);

        return new Success<Guid>(correlationId, ResultStatus.Accepted);
    }
}
