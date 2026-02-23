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

using SportsData.Core.Infrastructure.Refs;
using SportsData.Core.Infrastructure.DataSources.Espn;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.EventCompetitionStatus)]
public class EventCompetitionStatusDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : TeamSportDataContext
{
    public EventCompetitionStatusDocumentProcessor(
        ILogger<EventCompetitionStatusDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refGenerator)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refGenerator)
    {
    }

    protected override async Task ProcessInternal(ProcessDocumentCommand command)
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

        var competitionId = TryGetOrDeriveParentId(
            command,
            EspnUriMapper.CompetitionStatusRefToCompetitionRef);

        if (competitionId == null)
        {
            _logger.LogError("Unable to determine CompetitionId from ParentId or URI");
            throw new InvalidOperationException("CompetitionId (ParentId) is required to process CompetitionStatus");
        }

        var competitionIdValue = competitionId.Value;

        var entity = dto.AsEntity(
            _externalRefIdentityGenerator,
            competitionIdValue,
            command.CorrelationId);

        var existing = await _dataContext.CompetitionStatuses
            .Include(x => x.ExternalIds)
            .Include(x => x.Competition)
            .FirstOrDefaultAsync(x => x.CompetitionId == competitionIdValue);

        if (existing is not null)
        {
            publishEvent = existing.StatusTypeName != dto.Type.Name;

            _logger.LogInformation("Updating CompetitionStatus (hard replace). CompetitionId={CompId}, OldStatus={OldStatus}, NewStatus={NewStatus}", 
                competitionIdValue,
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
                competitionIdValue,
                entity.StatusTypeName,
                _refGenerator.ForCompetition(competitionIdValue),
                command.Sport,
                command.Season,
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