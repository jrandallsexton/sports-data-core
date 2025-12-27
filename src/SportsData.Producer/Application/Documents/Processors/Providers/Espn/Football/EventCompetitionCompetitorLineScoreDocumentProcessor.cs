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

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.EventCompetitionCompetitorLineScore)]
public class EventCompetitionCompetitorLineScoreDocumentProcessor<TDataContext> : IProcessDocuments
    where TDataContext : TeamSportDataContext
{
    private readonly ILogger<EventCompetitionCompetitorLineScoreDocumentProcessor<TDataContext>> _logger;
    private readonly TDataContext _dataContext;
    private readonly IEventBus _bus;
    private readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;
    private readonly DocumentProcessingConfig _config;

    public EventCompetitionCompetitorLineScoreDocumentProcessor(
        ILogger<EventCompetitionCompetitorLineScoreDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus bus,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        DocumentProcessingConfig config)
    {
        _logger = logger;
        _dataContext = dataContext;
        _bus = bus;
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
                   ["CompetitorId"] = command.ParentId ?? "Unknown"
               }))
        {
            _logger.LogInformation("EventCompetitionCompetitorLineScoreDocumentProcessor started. {@Command}", command);

            try
            {
                await ProcessInternal(command);
                
                _logger.LogInformation("EventCompetitionCompetitorLineScoreDocumentProcessor completed.");
            }
            catch (ExternalDocumentNotSourcedException retryEx)
            {
                _logger.LogWarning(retryEx, "Dependency not ready, will retry later.");
                
                var docCreated = command.ToDocumentCreated(command.AttemptCount + 1);
                await _bus.Publish(docCreated);
                await _dataContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EventCompetitionCompetitorLineScoreDocumentProcessor failed.");
                throw;
            }
        }
    }

    private async Task ProcessInternal(ProcessDocumentCommand command)
    {
        var dto = command.Document.FromJson<EspnEventCompetitionCompetitorLineScoreDto>();

        if (dto is null)
        {
            _logger.LogWarning("No line score found to process.");
            return;
        }

        if (!Guid.TryParse(command.ParentId, out var competitionCompetitorId))
        {
            _logger.LogError("ParentId must be a valid Guid for CompetitionCompetitorId. ParentId={ParentId}", command.ParentId);
            return; // fatal. do not retry
        }

        var exists = await _dataContext.CompetitionCompetitors
            .AsNoTracking()
            .AnyAsync(x => x.Id == competitionCompetitorId);

        if (!exists)
        {
            var competitionCompetitorRef = EspnUriMapper.CompetitionLineScoreRefToCompetitionCompetitorRef(dto.Ref);
            var competitionCompetitorIdentity = _externalRefIdentityGenerator.Generate(competitionCompetitorRef);

            var competitionRef = EspnUriMapper.CompetitionLineScoreRefToCompetitionRef(dto.Ref);
            var competitionIdentity = _externalRefIdentityGenerator.Generate(competitionRef);

            if (!_config.EnableDependencyRequests)
            {
                _logger.LogWarning(
                    "Missing dependency: {MissingDependencyType}. Processor: {ProcessorName}. Will retry. EnableDependencyRequests=false. Ref={Ref}",
                    DocumentType.EventCompetitionCompetitor,
                    nameof(EventCompetitionCompetitorLineScoreDocumentProcessor<TDataContext>),
                    competitionCompetitorRef);
                throw new ExternalDocumentNotSourcedException(
                    $"No CompetitionCompetitor exists with ID: {competitionCompetitorId}");
            }
            else
            {
                _logger.LogWarning("CompetitionCompetitor not found, raising DocumentRequested. CompetitorId={CompetitorId}", 
                    competitionCompetitorId);
                
                await _bus.Publish(new DocumentRequested(
                    Id: competitionCompetitorIdentity.UrlHash,
                    ParentId: competitionIdentity.CanonicalId.ToString(),
                    Uri: competitionCompetitorRef,
                    Sport: command.Sport,
                    SeasonYear: command.Season,
                    DocumentType: DocumentType.EventCompetitionCompetitor,
                    SourceDataProvider: command.SourceDataProvider,
                    CorrelationId: command.CorrelationId,
                    CausationId: CausationId.Producer.EventCompetitionCompetitorLineScoreDocumentProcessor
                ));

                throw new ExternalDocumentNotSourcedException($"No CompetitionCompetitor exists with ID: {competitionCompetitorId}");
            }
        }

        var identity = _externalRefIdentityGenerator.Generate(dto.Ref);
        var entry = await _dataContext.CompetitionCompetitorLineScores
            .AsTracking()
            .FirstOrDefaultAsync(x => x.Id == identity.CanonicalId);

        if (entry is not null)
        {
            _logger.LogInformation("Updating existing CompetitorLineScore. CompetitorId={CompetitorId}, Period={Period}", 
                competitionCompetitorId, 
                dto.Period);

            entry.Value = dto.Value;
            entry.DisplayValue = dto.DisplayValue;
            entry.Period = dto.Period;
            entry.SourceId = dto.Source?.Id ?? string.Empty;
            entry.SourceDescription = dto.Source?.Description ?? string.Empty;
            entry.SourceState = dto.Source?.State;
            entry.ModifiedUtc = DateTime.UtcNow;
            entry.ModifiedBy = command.CorrelationId;
        }
        else
        {
            _logger.LogInformation("Creating new CompetitorLineScore. CompetitorId={CompetitorId}, Period={Period}", 
                competitionCompetitorId, 
                dto.Period);

            var entity = dto.AsEntity(
                competitionCompetitorId,
                _externalRefIdentityGenerator,
                command.SourceDataProvider,
                command.CorrelationId);

            await _dataContext.CompetitionCompetitorLineScores.AddAsync(entity);
        }

        await _dataContext.SaveChangesAsync();
        
        _logger.LogInformation("Persisted CompetitorLineScore. CompetitorId={CompetitorId}, Period={Period}, Value={Value}", 
            competitionCompetitorId, 
            dto.Period,
            dto.Value);
    }
}