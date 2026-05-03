using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Common;

// MLB intentionally absent here — its status payload carries baseball-only
// fields (halfInning, periodPrefix, featuredAthletes[]) that this
// processor wouldn't model. Routed to
// BaseballEventCompetitionStatusDocumentProcessor under Espn/Baseball/,
// which constructs BaseballCompetitionStatus instead of FootballCompetitionStatus.
[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.EventCompetitionStatus)]
[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNfl, DocumentType.EventCompetitionStatus)]
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

        var dto = command.Document.FromJson<EspnEventCompetitionStatusDtoBase>();

        if (dto is null)
        {
            _logger.LogError("Failed to deserialize EspnEventCompetitionStatusDtoBase.");
            return; // terminal failure — don't retry
        }

        if (string.IsNullOrEmpty(dto.Ref?.ToString()))
        {
            _logger.LogError("EspnEventCompetitionStatusDtoBase Ref is null or empty.");
            return; // terminal failure — don't retry
        }

        var competitionId = TryGetOrDeriveParentId(
            command,
            EspnUriMapper.CompetitionStatusRefToCompetitionRef);

        if (competitionId == null)
        {
            _logger.LogError("Unable to determine CompetitionId from ParentId or URI");
            return;
        }

        var competitionIdValue = competitionId.Value;

        var entity = dto.AsEntity(
            _externalRefIdentityGenerator,
            competitionIdValue,
            command.CorrelationId);

        // Status is queried via the typed Set so the result materializes
        // as FootballCompetitionStatus (the only concrete subclass
        // registered in FootballDataContext).
        var existing = await _dataContext.Set<FootballCompetitionStatus>()
            .Include(x => x.ExternalIds)
            .FirstOrDefaultAsync(x => x.CompetitionId == competitionIdValue);

        if (existing is not null)
        {
            publishEvent = existing.StatusTypeName != dto.Type.Name;

            _logger.LogInformation(
                "Updating CompetitionStatus (hard replace). CompetitionId={CompId}, OldStatus={OldStatus}, NewStatus={NewStatus}",
                competitionIdValue,
                existing.StatusTypeName,
                dto.Type.Name);

            // ExternalIds cascade-delete with the parent (configured on
            // CompetitionStatus.EntityConfiguration), so removing the
            // parent is sufficient.
            _dataContext.Set<FootballCompetitionStatus>().Remove(existing);
        }
        else
        {
            _logger.LogInformation(
                "Creating new CompetitionStatus. CompetitionId={CompId}, Status={Status}",
                competitionIdValue,
                dto.Type.Name);
        }

        if (publishEvent)
        {
            // ContestId is what crosses the service boundary — Competition
            // is a Producer-internal sub-aggregate. Projected read so we
            // don't pull the full Competition row.
            var contestId = await _dataContext.Competitions
                .Where(c => c.Id == competitionIdValue)
                .Select(c => c.ContestId)
                .FirstAsync();

            _logger.LogInformation(
                "Contest status changed, publishing event. ContestId={ContestId}, CompetitionId={CompId}, NewStatus={Status}",
                contestId,
                competitionIdValue,
                entity.StatusTypeName);

            await _publishEndpoint.Publish(new ContestStatusChanged(
                contestId,
                entity.StatusTypeName,
                _refGenerator.ForCompetition(competitionIdValue),
                command.Sport,
                command.SeasonYear,
                command.CorrelationId,
                CausationId.Producer.EventCompetitionStatusDocumentProcessor
            ));
        }

        await _dataContext.Set<FootballCompetitionStatus>().AddAsync(entity);
        await _dataContext.SaveChangesAsync();

        _logger.LogInformation(
            "Persisted CompetitionStatus. CompetitionId={CompId}, Status={Status}",
            competitionIdValue,
            entity.StatusTypeName);
    }
}
