using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.EventCompetitionStatus)]
public class EventCompetitionStatusDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : TeamSportDataContext
{
    public EventCompetitionStatusDocumentProcessor(
        ILogger<EventCompetitionStatusDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator)
    {
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
            _logger.LogInformation("EventCompetitionStatusDocumentProcessor started. Ref={Ref}, UrlHash={UrlHash}", 
                command.GetDocumentRef(),
                command.UrlHash);

            try
            {
                await ProcessInternal(command);
                
                _logger.LogInformation("EventCompetitionStatusDocumentProcessor completed.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EventCompetitionStatusDocumentProcessor failed.");
                throw;
            }
        }
    }

    private async Task ProcessInternal(ProcessDocumentCommand command)
    {
        var publishEvent = false;

        var dto = command.Document.FromJson<EspnEventCompetitionStatusDto>();

        if (dto is null)
        {
            _logger.LogError("Failed to deserialize EspnEventCompetitionStatusDto.");
            return; // terminal failure — don't retry
        }

        if (string.IsNullOrEmpty(dto.Ref?.ToString()))
        {
            _logger.LogError("EspnEventCompetitionStatusDto Ref is null or empty.");
            return; // terminal failure — don't retry
        }

        if (!Guid.TryParse(command.ParentId, out var competitionId))
        {
            _logger.LogError("ParentId is missing or invalid for CompetitionStatus. ParentId={ParentId}", command.ParentId);
            throw new InvalidOperationException("CompetitionId (ParentId) is required to process CompetitionStatus");
        }

        var entity = dto.AsEntity(
            _externalRefIdentityGenerator,
            competitionId,
            command.CorrelationId);

        var existing = await _dataContext.CompetitionStatuses
            .Include(x => x.ExternalIds)
            .Include(x => x.Competition)
            .FirstOrDefaultAsync(x => x.CompetitionId == competitionId);

        if (existing is not null)
        {
            publishEvent = existing.StatusTypeName != dto.Type.Name;

            _logger.LogInformation("Updating CompetitionStatus (hard replace). CompetitionId={CompId}, OldStatus={OldStatus}, NewStatus={NewStatus}", 
                competitionId,
                existing.StatusTypeName,
                dto.Type.Name);

            // Remove only the ExternalIds for the ESPN provider to avoid unique key constraint violations
            var espnExternalIds = existing.ExternalIds
                .Where(x => x.Provider == SourceDataProvider.Espn)
                .ToList();

            _dataContext.CompetitionStatusExternalIds.RemoveRange(espnExternalIds);

            _dataContext.CompetitionStatuses.Remove(existing);
        }
        else
        {
            _logger.LogInformation("Creating new CompetitionStatus. CompetitionId={CompId}, Status={Status}", 
                competitionId,
                dto.Type.Name);
        }

        if (publishEvent)
        {
            _logger.LogInformation("Competition status changed, publishing event. CompetitionId={CompId}, NewStatus={Status}",
                competitionId,
                entity.StatusTypeName);

            await _publishEndpoint.Publish(new CompetitionStatusChanged(
                competitionId,
                entity.StatusTypeName,
                command.CorrelationId,
                CausationId.Producer.EventCompetitionStatusDocumentProcessor
            ));
        }

        await _dataContext.CompetitionStatuses.AddAsync(entity);
        await _dataContext.SaveChangesAsync();

        _logger.LogInformation("Persisted CompetitionStatus. CompetitionId={CompId}, Status={Status}", 
            competitionId,
            entity.StatusTypeName);
    }
}