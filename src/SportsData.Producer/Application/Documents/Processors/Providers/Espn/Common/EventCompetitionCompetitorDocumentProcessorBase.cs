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

        await _dataContext.SaveChangesAsync();

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
