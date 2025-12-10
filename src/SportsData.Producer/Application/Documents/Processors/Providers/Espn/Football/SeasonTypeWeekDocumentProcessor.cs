using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Config;
using SportsData.Producer.Exceptions;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.SeasonTypeWeek)]
public class SeasonTypeWeekDocumentProcessor<TDataContext> : IProcessDocuments
    where TDataContext : BaseDataContext
{
    private readonly ILogger<SeasonTypeWeekDocumentProcessor<TDataContext>> _logger;
    private readonly TDataContext _dataContext;
    private readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;
    private readonly IEventBus _publishEndpoint;
    private readonly DocumentProcessingConfig _config;

    public SeasonTypeWeekDocumentProcessor(
        ILogger<SeasonTypeWeekDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IEventBus publishEndpoint,
        DocumentProcessingConfig config)
    {
        _logger = logger;
        _dataContext = dataContext;
        _externalRefIdentityGenerator = externalRefIdentityGenerator;
        _publishEndpoint = publishEndpoint;
        _config = config;
    }

    public async Task ProcessAsync(ProcessDocumentCommand command)
    {
        using (_logger.BeginScope(new Dictionary<string, object> {
                   ["CorrelationId"] = command.CorrelationId,
                   ["OriginalUri"] = command.OriginalUri is null ? string.Empty : command.OriginalUri.ToString()
               }))
        {
            _logger.LogInformation("Began with {@command}", command);

            try
            {
                await ProcessInternal(command);
            }
            catch (ExternalDocumentNotSourcedException retryEx)
            {
                _logger.LogWarning(retryEx, "Dependency not ready. Will retry later.");
                var docCreated = command.ToDocumentCreated(command.AttemptCount + 1);
                await _publishEndpoint.Publish(docCreated);
                await _dataContext.OutboxPings.AddAsync(new OutboxPing());
                await _dataContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing. {@Command}", command);
                throw;
            }
        }
    }

    private async Task ProcessInternal(ProcessDocumentCommand command)
    {
        if (command.Season is null) 
        {
            _logger.LogError("Command does not contain a valid Season. {@Command}", command);
            return;
        }

        if (!Guid.TryParse(command.ParentId, out var seasonPhaseId))
        {
            _logger.LogError("SeasonPhaseId could not be parsed");
            return;
        }

        var externalProviderDto = command.Document.FromJson<EspnFootballSeasonTypeWeekDto>();

        if (externalProviderDto is null)
        {
            _logger.LogError("Failed to deserialize document to EspnFootballSeasonTypeWeekDto. {@Command}", command);
            return;
        }

        if (string.IsNullOrEmpty(externalProviderDto.Ref?.ToString()))
        {
            _logger.LogError("EspnFootballSeasonTypeWeekDto Ref is null or empty. {@Command}", command);
            return;
        }

        var seasonPhase = await _dataContext.SeasonPhases
            .Include(x => x.Weeks)
            .ThenInclude(w => w.ExternalIds)
            .Where(x => x.Id == seasonPhaseId)
            .FirstOrDefaultAsync();

        if (seasonPhase == null)
        {
            var seasonPhaseRef = EspnUriMapper.SeasonTypeWeekToSeasonType(externalProviderDto.Ref);
            var seasonPhaseIdentity = _externalRefIdentityGenerator.Generate(seasonPhaseRef);

            if (!_config.EnableDependencyRequests)
            {
                _logger.LogWarning(
                    "Missing dependency: {MissingDependencyType}. Processor: {ProcessorName}. Will retry. EnableDependencyRequests=false. Ref={Ref}",
                    DocumentType.SeasonType,
                    nameof(SeasonTypeWeekDocumentProcessor<TDataContext>),
                    seasonPhaseRef);
                throw new ExternalDocumentNotSourcedException(
                    $"SeasonPhase {seasonPhaseRef} not found. Will retry.");
            }
            else
            {
                // Legacy mode: keep existing DocumentRequested logic
                _logger.LogWarning(
                    "SeasonPhase not found. Raising DocumentRequested (override mode). SeasonPhaseId={SeasonPhaseId}",
                    seasonPhaseId);
                
                await _publishEndpoint.Publish(new DocumentRequested(
                    Id: seasonPhaseIdentity.UrlHash,
                    ParentId: null,
                    Uri: seasonPhaseRef,
                    Sport: command.Sport,
                    SeasonYear: command.Season,
                    DocumentType: DocumentType.SeasonType,
                    SourceDataProvider: command.SourceDataProvider,
                    CorrelationId: command.CorrelationId,
                    CausationId: CausationId.Producer.SeasonTypeWeekDocumentProcessor
                ));
                await _dataContext.OutboxPings.AddAsync(new OutboxPing());
                await _dataContext.SaveChangesAsync();

                throw new ExternalDocumentNotSourcedException($"SeasonPhase {seasonPhaseRef} not found. Will retry.");
            }
        }

        var dtoIdentity = _externalRefIdentityGenerator.Generate(externalProviderDto.Ref);

        var seasonWeek = seasonPhase.Weeks
            .FirstOrDefault(w => w.ExternalIds.Any(id => id.SourceUrlHash == dtoIdentity.UrlHash &&
                                                         id.Provider == command.SourceDataProvider));
        if (seasonWeek is null)
        {
            await ProcessNewEntity(externalProviderDto, seasonPhase, command, dtoIdentity);
        }
        else
        {
            await ProcessExistingEntity();
        }

    }

    private async Task ProcessNewEntity(
        EspnFootballSeasonTypeWeekDto dto,
        SeasonPhase seasonPhase,
        ProcessDocumentCommand command,
        ExternalRefIdentity dtoIdentity)
    {
        var seasonWeek = dto.AsEntity(
            seasonPhase.SeasonId,
            seasonPhase.Id,
            _externalRefIdentityGenerator,
            command.CorrelationId);

        // Note: Rankings are available here, but we skip processing them for now

        await _dataContext.SeasonWeeks.AddAsync(seasonWeek);
        await _dataContext.SaveChangesAsync();
    }

    private async Task ProcessExistingEntity()
    {
        _logger.LogError("Update detected. Not implemented");
        await Task.Delay(100);
    }
}