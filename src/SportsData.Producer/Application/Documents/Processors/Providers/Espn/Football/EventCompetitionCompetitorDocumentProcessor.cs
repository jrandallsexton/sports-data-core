using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Config;
using SportsData.Producer.Exceptions;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.EventCompetitionCompetitor)]
public class EventCompetitionCompetitorDocumentProcessor<TDataContext> : IProcessDocuments
    where TDataContext : TeamSportDataContext
{
    private readonly ILogger<EventCompetitionCompetitorDocumentProcessor<TDataContext>> _logger;
    private readonly TDataContext _dataContext;
    private readonly IEventBus _publishEndpoint;
    private readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;
    private readonly DocumentProcessingConfig _config;

    public EventCompetitionCompetitorDocumentProcessor(
        ILogger<EventCompetitionCompetitorDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        DocumentProcessingConfig config)
    {
        _logger = logger;
        _dataContext = dataContext;
        _publishEndpoint = publishEndpoint;
        _externalRefIdentityGenerator = externalRefIdentityGenerator;
        _config = config;
    }

    public async Task ProcessAsync(ProcessDocumentCommand command)
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

                await _publishEndpoint.Publish(new DocumentRequested(
                    Id: competitionIdentity.UrlHash,
                    ParentId: contestIdentity.CanonicalId.ToString(),
                    Uri: competitionRef,
                    Sport: command.Sport,
                    SeasonYear: command.Season,
                    DocumentType: DocumentType.EventCompetition,
                    SourceDataProvider: command.SourceDataProvider,
                    CorrelationId: command.CorrelationId,
                    CausationId: CausationId.Producer.EventCompetitionCompetitorDocumentProcessor
                ));



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
            "?? SAVING_CHANGES: About to call SaveChangesAsync to persist CompetitionCompetitor and flush outbox. " +
            "CompetitionId={CompetitionId}, HasPendingChanges={HasChanges}",
            competitionId,
            _dataContext.ChangeTracker.HasChanges());

        await _dataContext.SaveChangesAsync();

        _logger.LogInformation(
            "? SAVE_COMPLETED: SaveChangesAsync completed. All outbox messages should now be flushed to service bus. " +
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
            "?? CREATE_COMPETITOR: Creating new CompetitionCompetitor. " +
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
            "? COMPETITOR_CREATED: CompetitionCompetitor entity created. " +
            "CompetitorId={CompetitorId}, CompetitionId={CompetitionId}",
            canonicalEntity.Id,
            competitionId);

        _logger.LogInformation(
            "?? PROCESS_DOWNSTREAM: Processing downstream documents (Scores and LineScores). CompetitorId={CompetitorId}",
            canonicalEntity.Id);

        await ProcessScores(canonicalEntity.Id, dto, command);
        await ProcessLineScores(canonicalEntity.Id, dto, command);

        _logger.LogInformation(
            "? DOWNSTREAM_COMPLETED: Downstream processing completed. CompetitorId={CompetitorId}",
            canonicalEntity.Id);

        // TODO: ProcessRoster
        // TODO: ProcessStatistics
        // TODO: ProcessLeaders
        // TODO: ProcessRecord
        // TODO: ProcessRanks
    }

    private async Task ProcessUpdate(
        ProcessDocumentCommand command,
        EspnEventCompetitionCompetitorDto dto,
        CompetitionCompetitor entity)
    {
        _logger.LogInformation(
            "?? UPDATE_COMPETITOR: Updating existing CompetitionCompetitor. " +
            "CompetitorId={CompetitorId}, HomeAway={HomeAway}",
            entity.Id,
            dto.HomeAway);

        _logger.LogInformation(
            "?? PROCESS_DOWNSTREAM: Processing downstream documents (Scores and LineScores). CompetitorId={CompetitorId}",
            entity.Id);

        await ProcessScores(entity.Id, dto, command);
        await ProcessLineScores(entity.Id, dto, command);

        _logger.LogInformation(
            "? DOWNSTREAM_COMPLETED: Downstream processing completed. CompetitorId={CompetitorId}",
            entity.Id);
    }

    private async Task ProcessScores(
        Guid competitionCompetitorId,
        EspnEventCompetitionCompetitorDto externalProviderDto,
        ProcessDocumentCommand command)
    {
        _logger.LogInformation(
            "?? PROCESS_SCORES: Checking for competitor score. CompetitorId={CompetitorId}, HasScoreRef={HasScoreRef}",
            competitionCompetitorId,
            externalProviderDto.Score?.Ref != null);

        if (externalProviderDto.Score?.Ref is null)
        {
            _logger.LogDebug(
                "?? SKIP_SCORES: No score reference found in DTO. CompetitorId={CompetitorId}",
                competitionCompetitorId);
            return;
        }

        var competitorScoreIdentity = _externalRefIdentityGenerator.Generate(externalProviderDto.Score.Ref);

        _logger.LogInformation(
            "?? PUBLISH_SCORE_REQUEST: Publishing DocumentRequested for competitor score. " +
            "CompetitorId={CompetitorId}, ScoreUrl={ScoreUrl}, UrlHash={UrlHash}",
            competitionCompetitorId,
            competitorScoreIdentity.CleanUrl,
            competitorScoreIdentity.UrlHash);

        await _publishEndpoint.Publish(new DocumentRequested(
            Id: competitorScoreIdentity.UrlHash,
            ParentId: competitionCompetitorId.ToString(),
            Uri: new Uri(competitorScoreIdentity.CleanUrl),
            Sport: Sport.FootballNcaa,
            SeasonYear: command.Season,
            DocumentType: DocumentType.EventCompetitionCompetitorScore,
            SourceDataProvider: SourceDataProvider.Espn,
            CorrelationId: command.CorrelationId,
            CausationId: CausationId.Producer.EventCompetitionCompetitorDocumentProcessor
        ));

        _logger.LogInformation(
            "? SCORE_REQUEST_PUBLISHED: DocumentRequested published for competitor score. " +
            "CompetitorId={CompetitorId}, DocumentType={DocumentType}",
            competitionCompetitorId,
            DocumentType.EventCompetitionCompetitorScore);
    }

    private async Task ProcessLineScores(
        Guid competitionCompetitorId,
        EspnEventCompetitionCompetitorDto externalProviderDto,
        ProcessDocumentCommand command)
    {
        _logger.LogInformation(
            "?? PROCESS_LINESCORES: Checking for competitor line scores. CompetitorId={CompetitorId}, HasLineScoresRef={HasLineScoresRef}",
            competitionCompetitorId,
            externalProviderDto.Linescores?.Ref != null);

        if (externalProviderDto.Linescores?.Ref is null)
        {
            _logger.LogDebug(
                "?? SKIP_LINESCORES: No line scores reference found in DTO. CompetitorId={CompetitorId}",
                competitionCompetitorId);
            return;
        }

        var lineScoresIdentity = _externalRefIdentityGenerator.Generate(externalProviderDto.Linescores.Ref);

        _logger.LogInformation(
            "?? PUBLISH_LINESCORES_REQUEST: Publishing DocumentRequested for competitor line scores. " +
            "CompetitorId={CompetitorId}, LineScoresUrl={LineScoresUrl}, UrlHash={UrlHash}",
            competitionCompetitorId,
            lineScoresIdentity.CleanUrl,
            lineScoresIdentity.UrlHash);

        await _publishEndpoint.Publish(new DocumentRequested(
            Id: lineScoresIdentity.UrlHash,
            ParentId: competitionCompetitorId.ToString(),
            Uri: new Uri(lineScoresIdentity.CleanUrl),
            Sport: Sport.FootballNcaa,
            SeasonYear: command.Season,
            DocumentType: DocumentType.EventCompetitionCompetitorLineScore,
            SourceDataProvider: SourceDataProvider.Espn,
            CorrelationId: command.CorrelationId,
            CausationId: CausationId.Producer.EventCompetitionCompetitorDocumentProcessor
        ));

        _logger.LogInformation(
            "? LINESCORES_REQUEST_PUBLISHED: DocumentRequested published for competitor line scores. " +
            "CompetitorId={CompetitorId}, DocumentType={DocumentType}",
            competitionCompetitorId,
            DocumentType.EventCompetitionCompetitorLineScore);
    }
}
