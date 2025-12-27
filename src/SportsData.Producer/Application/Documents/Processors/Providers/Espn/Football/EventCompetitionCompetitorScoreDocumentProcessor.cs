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
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.EventCompetitionCompetitorScore)]
public class EventCompetitionCompetitorScoreDocumentProcessor<TDataContext> : IProcessDocuments
    where TDataContext : TeamSportDataContext
{
    private readonly ILogger<EventCompetitionCompetitorScoreDocumentProcessor<TDataContext>> _logger;
    private readonly TDataContext _dataContext;
    private readonly IEventBus _bus;
    private readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;
    private readonly DocumentProcessingConfig _config;

    public EventCompetitionCompetitorScoreDocumentProcessor(
        ILogger<EventCompetitionCompetitorScoreDocumentProcessor<TDataContext>> logger,
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
            _logger.LogInformation("EventCompetitionCompetitorScoreDocumentProcessor started. {@Command}", command);

            try
            {
                await ProcessInternal(command);
                
                _logger.LogInformation("EventCompetitionCompetitorScoreDocumentProcessor completed.");
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
                _logger.LogError(ex, "EventCompetitionCompetitorScoreDocumentProcessor failed.");
                throw;
            }
        }
    }

    private async Task ProcessInternal(ProcessDocumentCommand command)
    {
        var dto = command.Document.FromJson<EspnEventCompetitionCompetitorScoreDto>();

        if (dto is null)
        {
            _logger.LogError("Failed to deserialize EspnEventCompetitionCompetitorScoreDto.");
            return;
        }

        if (!Guid.TryParse(command.ParentId, out var competitionCompetitorId))
        {
            _logger.LogError("ParentId must be a valid Guid for CompetitionCompetitorId. ParentId={ParentId}", command.ParentId);
            throw new InvalidOperationException("Invalid ParentId for CompetitionCompetitorId");
        }

        var competitionCompetitorExists = await _dataContext.CompetitionCompetitors
            .AsNoTracking()
            .AnyAsync(x => x.Id == competitionCompetitorId);

        if (!competitionCompetitorExists)
        {
            var competitionCompetitorRef =
                EspnUriMapper.CompetitionCompetitorScoreRefToCompetitionCompetitorRef(dto.Ref);
            var competitionCompetitorIdentity =
                _externalRefIdentityGenerator.Generate(competitionCompetitorRef);

            var competitionRef =
                EspnUriMapper.CompetitionCompetitorRefToCompetitionRef(new Uri(competitionCompetitorIdentity
                    .CleanUrl));
            var competitionIdentity = _externalRefIdentityGenerator.Generate(competitionRef);

            if (!_config.EnableDependencyRequests)
            {
                _logger.LogWarning(
                    "Missing dependency: {MissingDependencyType}. Processor: {ProcessorName}. Will retry. EnableDependencyRequests=false. Ref={Ref}",
                    DocumentType.EventCompetitionCompetitor,
                    nameof(EventCompetitionCompetitorScoreDocumentProcessor<TDataContext>),
                    competitionCompetitorIdentity.CleanUrl);
                throw new ExternalDocumentNotSourcedException(
                    $"CompetitionCompetitor {competitionCompetitorIdentity.CleanUrl} not found. Will retry when available.");
            }
            else
            {
                _logger.LogWarning("CompetitionCompetitor not found, raising DocumentRequested. CompetitorUrl={CompetitorUrl}", 
                    competitionCompetitorIdentity.CleanUrl);

                await _bus.Publish(new DocumentRequested(
                    Id: competitionCompetitorIdentity.UrlHash,
                    ParentId: competitionIdentity.CanonicalId.ToString(),
                    Uri: new Uri(competitionCompetitorIdentity.CleanUrl),
                    Sport: command.Sport,
                    SeasonYear: command.Season,
                    DocumentType: DocumentType.EventCompetitionCompetitor,
                    SourceDataProvider: SourceDataProvider.Espn,
                    CorrelationId: command.CorrelationId,
                    CausationId: CausationId.Producer.EventCompetitionCompetitorScoreDocumentProcessor
                ));

                await _dataContext.SaveChangesAsync();

                throw new ExternalDocumentNotSourcedException($"CompetitionCompetitor {competitionCompetitorIdentity.CleanUrl} not found. Will retry.");
            }
        }

        var scoreIdentity = _externalRefIdentityGenerator.Generate(dto.Ref);

        var score = await _dataContext.CompetitionCompetitorScores
            .Where(x => x.Id == scoreIdentity.CanonicalId)
            .FirstOrDefaultAsync();

        if (score != null)
        {
            _logger.LogInformation("Updating existing CompetitorScore. CompetitorId={CompetitorId}, ScoreId={ScoreId}", 
                competitionCompetitorId, 
                scoreIdentity.CanonicalId);

            score.Value = dto.Value;
            score.DisplayValue = dto.DisplayValue;
            score.ModifiedBy = command.CorrelationId;
            score.ModifiedUtc = DateTime.UtcNow;
            score.SourceDescription = dto.Source?.Description ?? score.SourceDescription;
        }
        else
        {
            _logger.LogInformation("Creating new CompetitorScore. CompetitorId={CompetitorId}", competitionCompetitorId);

            var entity = dto.AsEntity(
                competitionCompetitorId,
                _externalRefIdentityGenerator,
                command.SourceDataProvider,
                command.CorrelationId);

            await _dataContext.CompetitionCompetitorScores.AddAsync(entity);
        }

        await _dataContext.SaveChangesAsync();

        _logger.LogInformation("Persisted CompetitorScore. CompetitorId={CompetitorId}, Value={Value}", 
            competitionCompetitorId, 
            dto.Value);
    }
}