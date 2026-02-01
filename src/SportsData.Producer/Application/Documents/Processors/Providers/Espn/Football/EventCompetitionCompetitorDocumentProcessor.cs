using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Config;
using SportsData.Producer.Exceptions;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.EventCompetitionCompetitor)]
public class EventCompetitionCompetitorDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : TeamSportDataContext
{
    private readonly DocumentProcessingConfig _config;

    public EventCompetitionCompetitorDocumentProcessor(
        ILogger<EventCompetitionCompetitorDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refs,
        DocumentProcessingConfig config)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refs)
    {
        _config = config;
    }

    public override async Task ProcessAsync(ProcessDocumentCommand command)
    {
        using (_logger.BeginScope(new Dictionary<string, object>
               {
                   ["CorrelationId"] = command.CorrelationId,
                   ["DocumentType"] = command.DocumentType,
                   ["Season"] = command.Season ?? 0,
                   ["CompetitionId"] = command.ParentId ?? "Unknown"
               }))
        {
            _logger.LogInformation("EventCompetitionCompetitorDocumentProcessor started. Ref={Ref}, UrlHash={UrlHash}", 
                command.GetDocumentRef(),
                command.UrlHash);

            try
            {
                await ProcessInternal(command);
                
                _logger.LogInformation("EventCompetitionCompetitorDocumentProcessor completed.");
            }
            catch (ExternalDocumentNotSourcedException retryEx)
            {
                _logger.LogWarning(retryEx, "Dependency not ready, will retry later.");
                
                var docCreated = command.ToDocumentCreated(command.AttemptCount + 1);
                await _publishEndpoint.Publish(docCreated);
                await _dataContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EventCompetitionCompetitorDocumentProcessor failed.");
                throw;
            }
        }
    }

    private async Task ProcessInternal(ProcessDocumentCommand command)
    {
        var dto = command.Document.FromJson<EspnEventCompetitionCompetitorDto>();

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

        if (!command.Season.HasValue)
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

            if (!_config.EnableDependencyRequests)
            {
                _logger.LogWarning(
                    "Missing dependency: {MissingDependencyType}. Processor: {ProcessorName}. Will retry. EnableDependencyRequests=false. Ref={Ref}",
                    DocumentType.EventCompetition,
                    nameof(EventCompetitionCompetitorDocumentProcessor<TDataContext>),
                    competitionRef);
                throw new ExternalDocumentNotSourcedException(
                    $"Competition with ID {competitionId} does not exist.");
            }
            else
            {
                _logger.LogWarning("Competition not found, raising DocumentRequested. CompetitionId={CompetitionId}", competitionId);

                await PublishChildDocumentRequest(
                    command,
                    new EspnLinkDto { Ref = competitionRef },
                    contestIdentity.CanonicalId.ToString(),
                    DocumentType.EventCompetition,
                    CausationId.Producer.EventCompetitionCompetitorDocumentProcessor);

                throw new ExternalDocumentNotSourcedException($"Competition with ID {competitionId} does not exist.");
            }
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

        var canonicalEntity = dto.AsEntity(
            competitionId,
            franchiseSeasonId,
            _externalRefIdentityGenerator,
            command.CorrelationId);

        await _dataContext.CompetitionCompetitors.AddAsync(canonicalEntity);

        _logger.LogInformation(
            "✅ COMPETITOR_CREATED: CompetitionCompetitor entity created. " +
            "CompetitorId={CompetitorId}, CompetitionId={CompetitionId}",
            canonicalEntity.Id,
            competitionId);

        await ProcessChildDocuments(command, dto, canonicalEntity.Id, isNew: true);
    }

    private async Task ProcessUpdate(
        ProcessDocumentCommand command,
        EspnEventCompetitionCompetitorDto dto,
        CompetitionCompetitor entity)
    {
        _logger.LogInformation(
            "🔄 UPDATE_COMPETITOR: Updating existing CompetitionCompetitor. " +
            "CompetitorId={CompetitorId}, HomeAway={HomeAway}",
            entity.Id,
            dto.HomeAway);

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
                DocumentType.EventCompetitionCompetitorScore,
                CausationId.Producer.EventCompetitionCompetitorDocumentProcessor);

        if (isNew || ShouldSpawn(DocumentType.EventCompetitionCompetitorLineScore, command))
            await PublishChildDocumentRequest(command, dto.Linescores, competitorId,
                DocumentType.EventCompetitionCompetitorLineScore,
                CausationId.Producer.EventCompetitionCompetitorDocumentProcessor);

        if (isNew || ShouldSpawn(DocumentType.EventCompetitionCompetitorRoster, command))
            await PublishChildDocumentRequest(command, dto.Roster, competitorId,
                DocumentType.EventCompetitionCompetitorRoster,
                CausationId.Producer.EventCompetitionCompetitorDocumentProcessor);

        if (isNew || ShouldSpawn(DocumentType.EventCompetitionCompetitorStatistics, command))
            await PublishChildDocumentRequest(command, dto.Statistics, competitorId,
                DocumentType.EventCompetitionCompetitorStatistics,
                CausationId.Producer.EventCompetitionCompetitorDocumentProcessor);

        if (isNew || ShouldSpawn(DocumentType.EventCompetitionCompetitorRecord, command))
            await PublishChildDocumentRequest(command, dto.Record, competitorId,
                DocumentType.EventCompetitionCompetitorRecord,
                CausationId.Producer.EventCompetitionCompetitorDocumentProcessor);

        _logger.LogInformation(
            "✅ CHILD_DOCUMENTS_COMPLETED: Child document processing completed. CompetitorId={CompetitorId}",
            competitorId);
    }
}
