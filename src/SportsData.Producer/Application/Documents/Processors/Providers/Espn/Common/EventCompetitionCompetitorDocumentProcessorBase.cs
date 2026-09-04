using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Exceptions;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Common;

// Abstract base for the per-sport EventCompetitionCompetitor processors.
// Shared work — competition existence guard, FranchiseSeason resolve,
// existing-entity lookup, child-doc spawning, save — lives here. The
// concrete-entity construction (CompetitionCompetitorBase subclass) is
// deferred to CreateEntity, overridden by each sport-specific subclass.
//
// See docs/competition-competitor-split.md.
public abstract class EventCompetitionCompetitorDocumentProcessorBase<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : TeamSportDataContext
{
    /// <summary>
    /// Transient HomeAway value parking OUR row during a full home/away
    /// swap (see the re-designation block in ProcessInternal). MUST fit
    /// the column's varchar(10) — the PostgresException the length test
    /// pins is exactly what a longer value did in production.
    /// </summary>
    public const string SwapParkingValue = "swap";

    protected EventCompetitionCompetitorDocumentProcessorBase(
        ILogger logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refs)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refs) { }

    /// <summary>
    /// Construct the sport-specific concrete CompetitionCompetitorBase
    /// subclass from the DTO. Subclasses set their sport-only fields here
    /// (e.g. FootballCompetitionCompetitor.CuratedRankCurrent) in addition
    /// to the shared columns.
    /// </summary>
    protected abstract CompetitionCompetitorBase CreateEntity(
        EspnEventCompetitionCompetitorDto dto,
        Guid competitionId,
        Guid franchiseSeasonId,
        Guid correlationId);

    /// <summary>
    /// DTO deserializer hook. Override in sports that ship inline
    /// extras on the competitor payload (e.g. MLB Probables) so the
    /// override returns the sport-specific subclass DTO. The base
    /// pipeline then passes that instance through to ProcessSportSpecific*
    /// hooks where the override can downcast and act on the extras.
    /// </summary>
    protected virtual EspnEventCompetitionCompetitorDto? DeserializeDto(string document)
        => document.FromJson<EspnEventCompetitionCompetitorDto>();

    /// <summary>
    /// Sport-specific hook for inline-data ingestion that hangs off the
    /// competitor entity (e.g. MLB Probables). Runs after the competitor
    /// row is staged on the change tracker but before SaveChangesAsync,
    /// so any rows added by the override commit in the same transaction.
    /// Throwing ExternalDocumentNotSourcedException here is supported and
    /// expected when a referenced dependency (e.g. AthleteSeason) isn't
    /// in the DB yet — Hangfire will retry the document.
    /// </summary>
    protected virtual Task ProcessSportSpecificCompetitorData(
        ProcessDocumentCommand command,
        EspnEventCompetitionCompetitorDto dto,
        CompetitionCompetitorBase entity) => Task.CompletedTask;

    protected override async Task ProcessInternal(ProcessDocumentCommand command)
    {
        var dto = DeserializeDto(command.Document);

        if (dto is null)
        {
            _logger.LogError("Failed to deserialize EspnEventCompetitionCompetitorDto.");
            return;
        }

        if (string.IsNullOrWhiteSpace(dto.Ref?.ToString()))
        {
            _logger.LogError("EspnEventCompetitionCompetitorDto Ref is null.");
            return;
        }

        if (!command.SeasonYear.HasValue)
        {
            _logger.LogError("Command missing SeasonYear.");
            return;
        }

        if (string.IsNullOrWhiteSpace(command.ParentId))
        {
            _logger.LogError("Command missing ParentId for CompetitionId.");
            return;
        }

        if (!Guid.TryParse(command.ParentId, out var competitionId))
        {
            _logger.LogError("CompetitionId could not be parsed. ParentId={ParentId}", command.ParentId);
            return;
        }

        var competitionExists = await _dataContext.Competitions
            .AsNoTracking()
            .AnyAsync(x => x.Id == competitionId);

        if (!competitionExists)
        {
            var competitionRef = EspnUriMapper.CompetitionCompetitorRefToCompetitionRef(dto.Ref);
            var competitionIdentity = _externalRefIdentityGenerator.Generate(competitionRef);

            var contestRef = EspnUriMapper.CompetitionRefToContestRef(competitionRef);
            var contestIdentity = _externalRefIdentityGenerator.Generate(contestRef);

            await PublishDependencyRequest<Guid>(
                command,
                new EspnLinkDto { Ref = competitionRef },
                parentId: contestIdentity.CanonicalId,
                DocumentType.EventCompetition);

            throw new ExternalDocumentNotSourcedException($"Competition with ID {competitionId} does not exist. Requested. Will retry.");
        }

        var franchiseSeasonId = await _dataContext.ResolveIdAsync<
            FranchiseSeason, FranchiseSeasonExternalId>(
            dto.Team,
            command.SourceDataProvider,
            () => _dataContext.FranchiseSeasons,
            externalIdsNav: "ExternalIds",
            key: fs => fs.Id);

        if (franchiseSeasonId is null)
        {
            _logger.LogError("FranchiseSeason could not be resolved. DtoRef={DtoRef}", dto.Team?.Ref);
            throw new InvalidOperationException("FranchiseSeason could not be resolved from DTO reference.");
        }

        var entity = await _dataContext.CompetitionCompetitors
            .Include(x => x.ExternalIds)
            .FirstOrDefaultAsync(x =>
                x.ExternalIds.Any(z => z.SourceUrlHash == command.UrlHash &&
                                       z.Provider == command.SourceDataProvider));

        // ESPN home/away re-designation swaps sides on BOTH competitors, but
        // documents arrive one at a time — the first writer collides with the
        // STALE occupant of its new side under the (CompetitionId, HomeAway)
        // unique index (2026-08-29 Howard @ Alabama A&M: six Hangfire jobs
        // retrying into the same stale row; the stale side also poisoned
        // score-side attribution downstream). A competition has exactly two
        // competitors, so the occupant of our target side belongs on the
        // other one: relocate it FIRST, in its own SaveChanges, so the slot
        // is free before our write. Each step is idempotent under Hangfire
        // retries — a crash at any point converges on reprocessing.
        var desiredSide = dto.HomeAway?.Trim().ToLowerInvariant();
        if (desiredSide is "home" or "away")
        {
            var occupant = await _dataContext.CompetitionCompetitors
                .FirstOrDefaultAsync(x =>
                    x.CompetitionId == competitionId
                    && x.HomeAway == desiredSide
                    && x.FranchiseSeasonId != franchiseSeasonId.Value);

            if (occupant is not null)
            {
                var otherSide = desiredSide == "home" ? "away" : "home";

                // Full-swap case: OUR row currently holds the other side —
                // park it out of the way first or the occupant's relocation
                // collides with it. ProcessUpdate below writes the final side.
                // Parking value MUST fit HomeAway's varchar(10) — the first
                // deploy used "swap-pending" (12 chars) and every full-swap
                // doc failed with 22001; InMemory tests can't catch length
                // violations, so the length is asserted in the test suite.
                if (entity is not null
                    && string.Equals(entity.HomeAway, otherSide, StringComparison.OrdinalIgnoreCase))
                {
                    entity.HomeAway = SwapParkingValue;
                    await _dataContext.SaveChangesAsync();
                }

                _logger.LogWarning(
                    "Home/away re-designation: relocating stale occupant of side '{Side}' to '{OtherSide}'. " +
                    "CompetitionId={CompetitionId}, OccupantId={OccupantId}, OccupantFranchiseSeasonId={OccupantFranchiseSeasonId}",
                    desiredSide, otherSide, competitionId, occupant.Id, occupant.FranchiseSeasonId);

                occupant.HomeAway = otherSide;
                await _dataContext.SaveChangesAsync();
            }
        }

        if (entity is null)
        {
            _logger.LogInformation("Processing new CompetitionCompetitor entity. Ref={Ref}", dto.Ref);
            await ProcessNewEntity(command, dto, competitionId, franchiseSeasonId.Value);
        }
        else
        {
            _logger.LogInformation("Processing CompetitionCompetitor update. CompetitorId={CompetitorId}, Ref={Ref}", entity.Id, dto.Ref);
            await ProcessUpdate(command, dto, entity);
        }

        _logger.LogInformation(
            "💾 SAVING_CHANGES: About to call SaveChangesAsync to persist CompetitionCompetitor and flush outbox. " +
            "CompetitionId={CompetitionId}, HasPendingChanges={HasChanges}",
            competitionId,
            _dataContext.ChangeTracker.HasChanges());

        try
        {
            await _dataContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
        {
            // At-least-once delivery + parallel Hangfire workers can process the same
            // competitor document concurrently. Both run SELECT-then-INSERT for the
            // competitor and its deterministic-Id child rows (e.g. the MLB
            // CompetitionCompetitorProbable), and the loser hits a duplicate-key here.
            // Recover only if EVERY row this batch attempted to insert is already present
            // in the database (the winner wrote the identical deterministic-Id rows) —
            // narrower than "the competitor exists", which is trivially true on the
            // update path and would mask an unrelated violation.
            var recovered = await TryRecoverFromDuplicateInsertAsync(
                async () =>
                {
                    var added = _dataContext.ChangeTracker.Entries()
                        .Where(e => e.State == EntityState.Added)
                        .ToList();
                    if (added.Count == 0)
                        return false;
                    foreach (var entry in added)
                    {
                        if (await entry.GetDatabaseValuesAsync() is null)
                            return false;
                    }
                    return true;
                },
                $"CompetitionCompetitor CompetitionId={competitionId}, UrlHash={command.UrlHash}, CorrelationId={command.CorrelationId}");

            if (!recovered)
            {
                _logger.LogError(ex,
                    "Unique constraint violation persisting CompetitionCompetitor but not all attempted rows were " +
                    "found in the database — unrelated data-integrity issue. UrlHash={UrlHash}, CorrelationId={CorrelationId}",
                    command.UrlHash, command.CorrelationId);
                throw;
            }

            return;
        }

        _logger.LogInformation(
            "✅ SAVE_COMPLETED: SaveChangesAsync completed. All outbox messages should now be flushed to service bus. " +
            "CompetitionId={CompetitionId}",
            competitionId);
    }

    private async Task ProcessNewEntity(
        ProcessDocumentCommand command,
        EspnEventCompetitionCompetitorDto dto,
        Guid competitionId,
        Guid franchiseSeasonId)
    {
        _logger.LogInformation(
            "🆕 CREATE_COMPETITOR: Creating new CompetitionCompetitor. " +
            "CompetitionId={CompetitionId}, FranchiseSeasonId={FranchiseSeasonId}, HomeAway={HomeAway}",
            competitionId,
            franchiseSeasonId,
            dto.HomeAway);

        var canonicalEntity = CreateEntity(dto, competitionId, franchiseSeasonId, command.CorrelationId);

        await _dataContext.CompetitionCompetitors.AddAsync(canonicalEntity);

        _logger.LogInformation(
            "✅ COMPETITOR_CREATED: CompetitionCompetitor entity created. " +
            "CompetitorId={CompetitorId}, CompetitionId={CompetitionId}",
            canonicalEntity.Id,
            competitionId);

        // Sport-specific inline-data ingestion (e.g. MLB Probables). Runs
        // before child-doc spawning so any rows added share the tail
        // SaveChangesAsync transaction. May throw
        // ExternalDocumentNotSourcedException when a referenced
        // dependency isn't in the DB yet — Hangfire retries the document.
        await ProcessSportSpecificCompetitorData(command, dto, canonicalEntity);

        await ProcessChildDocuments(command, dto, canonicalEntity.Id, isNew: true);
    }

    private async Task ProcessUpdate(
        ProcessDocumentCommand command,
        EspnEventCompetitionCompetitorDto dto,
        CompetitionCompetitorBase entity)
    {
        _logger.LogInformation(
            "🔄 UPDATE_COMPETITOR: Updating existing CompetitionCompetitor. " +
            "CompetitorId={CompetitorId}, HomeAway={HomeAway}",
            entity.Id,
            dto.HomeAway);

        // Sync the mutable designations: a neutral-site home/away flip (2026
        // Wisconsin/Notre Dame at Lambeau), order, and winner must track
        // ESPN. Previously HomeAway was logged above but never WRITTEN,
        // leaving competitor rows frozen at first ingestion.
        var designationChanges = new List<string>();

        if (!string.IsNullOrWhiteSpace(dto.HomeAway) &&
            !string.Equals(entity.HomeAway, dto.HomeAway, StringComparison.OrdinalIgnoreCase))
        {
            designationChanges.Add($"HomeAway: {entity.HomeAway} -> {dto.HomeAway}");
            entity.HomeAway = dto.HomeAway;
        }

        if (entity.Order != dto.Order)
        {
            designationChanges.Add($"Order: {entity.Order} -> {dto.Order}");
            entity.Order = dto.Order;
        }

        if (entity.Winner != dto.Winner)
        {
            designationChanges.Add($"Winner: {entity.Winner} -> {dto.Winner}");
            entity.Winner = dto.Winner;
        }

        if (designationChanges.Count > 0)
        {
            _logger.LogWarning(
                "CompetitionCompetitor designations changed. CompetitorId={CompetitorId}, Changes={Changes}",
                entity.Id,
                string.Join(", ", designationChanges));
        }

        // Sport-specific inline-data ingestion runs on update too — the
        // payload's mutable extras (e.g. Probables) may have changed
        // since the last fetch.
        await ProcessSportSpecificCompetitorData(command, dto, entity);

        await ProcessChildDocuments(command, dto, entity.Id, isNew: false);
    }

    /// <summary>
    /// Processes all child documents for a competitor.
    /// For new entities (isNew=true), always spawns all child documents.
    /// For updates (isNew=false), respects ShouldSpawn filtering to prevent duplicate spawns.
    /// </summary>
    private async Task ProcessChildDocuments(
        ProcessDocumentCommand command,
        EspnEventCompetitionCompetitorDto dto,
        Guid competitorId,
        bool isNew)
    {
        _logger.LogInformation(
            "🔗 PROCESS_CHILD_DOCUMENTS: Processing child documents for competitor. CompetitorId={CompetitorId}, IsNew={IsNew}",
            competitorId,
            isNew);

        // All child documents - bypass ShouldSpawn for new entities, apply filtering for updates
        if (isNew || ShouldSpawn(DocumentType.EventCompetitionCompetitorScore, command))
            await PublishChildDocumentRequest(command, dto.Score, competitorId,
                DocumentType.EventCompetitionCompetitorScore);

        if (isNew || ShouldSpawn(DocumentType.EventCompetitionCompetitorLineScore, command))
            await PublishChildDocumentRequest(command, dto.Linescores, competitorId,
                DocumentType.EventCompetitionCompetitorLineScore);

        if (isNew || ShouldSpawn(DocumentType.EventCompetitionCompetitorRoster, command))
            await PublishChildDocumentRequest(command, dto.Roster, competitorId,
                DocumentType.EventCompetitionCompetitorRoster);

        if (isNew || ShouldSpawn(DocumentType.EventCompetitionCompetitorStatistics, command))
            await PublishChildDocumentRequest(command, dto.Statistics, competitorId,
                DocumentType.EventCompetitionCompetitorStatistics);

        if (isNew || ShouldSpawn(DocumentType.EventCompetitionCompetitorRecord, command))
            await PublishChildDocumentRequest(command, dto.Record, competitorId,
                DocumentType.EventCompetitionCompetitorRecord);

        _logger.LogInformation(
            "✅ CHILD_DOCUMENTS_COMPLETED: Child document processing completed. CompetitorId={CompetitorId}",
            competitorId);
    }
}
