using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Common;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

// MLB intentionally absent here — its status payload carries baseball-only
// fields (halfInning, periodPrefix, featuredAthletes[]) that this
// processor wouldn't model. Routed to
// BaseballEventCompetitionStatusDocumentProcessor under Espn/Baseball/,
// which constructs BaseballCompetitionStatus instead of FootballCompetitionStatus.
// Shared sport-agnostic lifecycle (ContestStatusChanged / ContestCompleted
// publishes + CancelledUtc stamp) lives on EventCompetitionStatusProcessorBase.
[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.EventCompetitionStatus)]
[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNfl, DocumentType.EventCompetitionStatus)]
public class FootballEventCompetitionStatusDocumentProcessor<TDataContext>
    : EventCompetitionStatusProcessorBase<TDataContext>
    where TDataContext : TeamSportDataContext
{
    public FootballEventCompetitionStatusDocumentProcessor(
        ILogger<FootballEventCompetitionStatusDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refGenerator,
        IDateTimeProvider dateTimeProvider)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refGenerator, dateTimeProvider)
    {
    }

    protected override async Task ProcessInternal(ProcessDocumentCommand command)
    {
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

        await HandleStatusLifecycleAsync(
            competitionId: competitionIdValue,
            newStatusTypeName: entity.StatusTypeName,
            newStatusDescription: entity.StatusDescription,
            existingStatusTypeName: existing?.StatusTypeName,
            command: command);

        await _dataContext.Set<FootballCompetitionStatus>().AddAsync(entity);
        await _dataContext.SaveChangesAsync();

        _logger.LogInformation(
            "Persisted CompetitionStatus. CompetitionId={CompId}, Status={Status}",
            competitionIdValue,
            entity.StatusTypeName);
    }
}
