using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Baseball;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Baseball;

/// <summary>
/// MLB-specific processor for EventCompetitionStatus documents.
///
/// Why a separate processor: ESPN's MLB status payload includes baseball-only
/// fields (<c>halfInning</c>, <c>periodPrefix</c>) and a <c>featuredAthletes</c>
/// child collection (current pitcher, batter, etc.) that the generic
/// <see cref="Common.EventCompetitionStatusDocumentProcessor{TDataContext}"/>
/// would silently drop because it deserializes to the base DTO. This processor
/// deserializes to <see cref="EspnBaseballEventCompetitionStatusDto"/> and
/// persists the full payload, hard-replacing the FeaturedAthletes collection
/// on every status update (consistent with the existing CompetitionStatus
/// remove+add pattern).
/// </summary>
[DocumentProcessor(SourceDataProvider.Espn, Sport.BaseballMlb, DocumentType.EventCompetitionStatus)]
public class BaseballEventCompetitionStatusDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : TeamSportDataContext
{
    public BaseballEventCompetitionStatusDocumentProcessor(
        ILogger<BaseballEventCompetitionStatusDocumentProcessor<TDataContext>> logger,
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

        var dto = command.Document.FromJson<EspnBaseballEventCompetitionStatusDto>();

        if (dto is null)
        {
            _logger.LogError("Failed to deserialize EspnBaseballEventCompetitionStatusDto.");
            return; // terminal failure — don't retry
        }

        if (string.IsNullOrEmpty(dto.Ref?.ToString()))
        {
            _logger.LogError("EspnBaseballEventCompetitionStatusDto Ref is null or empty.");
            return; // terminal failure — don't retry
        }

        var competitionId = TryGetOrDeriveParentId(
            command,
            EspnUriMapper.CompetitionStatusRefToCompetitionRef);

        if (competitionId is null)
        {
            _logger.LogError("Unable to determine CompetitionId from ParentId or URI");
            return;
        }

        var competitionIdValue = competitionId.Value;

        var entity = dto.AsEntity(
            _externalRefIdentityGenerator,
            competitionIdValue,
            command.CorrelationId);

        var existing = await _dataContext.CompetitionStatuses
            .Include(x => x.ExternalIds)
            .Include(x => x.FeaturedAthletes)
            .Include(x => x.Competition)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.CompetitionId == competitionIdValue);

        if (existing is not null)
        {
            publishEvent = existing.StatusTypeName != dto.Type.Name;

            _logger.LogInformation(
                "Updating MLB CompetitionStatus (hard replace). CompetitionId={CompId}, OldStatus={OldStatus}, NewStatus={NewStatus}, FeaturedAthleteCount={FeaturedAthleteCount}",
                competitionIdValue,
                existing.StatusTypeName,
                dto.Type.Name,
                entity.FeaturedAthletes.Count);

            // Remove only the ExternalIds for the ESPN provider to avoid
            // unique key constraint violations when the new entity re-adds
            // its own ESPN external id.
            var espnExternalIds = existing.ExternalIds
                .Where(x => x.Provider == SourceDataProvider.Espn)
                .ToList();

            _dataContext.CompetitionStatusExternalIds.RemoveRange(espnExternalIds);

            // FeaturedAthletes cascade-delete with the parent — no explicit
            // RemoveRange needed.
            _dataContext.CompetitionStatuses.Remove(existing);
        }
        else
        {
            _logger.LogInformation(
                "Creating new MLB CompetitionStatus. CompetitionId={CompId}, Status={Status}, FeaturedAthleteCount={FeaturedAthleteCount}",
                competitionIdValue,
                dto.Type.Name,
                entity.FeaturedAthletes.Count);
        }

        if (publishEvent)
        {
            _logger.LogInformation(
                "MLB Competition status changed, publishing event. CompetitionId={CompId}, NewStatus={Status}",
                competitionIdValue,
                entity.StatusTypeName);

            await _publishEndpoint.Publish(new CompetitionStatusChanged(
                competitionIdValue,
                entity.StatusTypeName,
                _refGenerator.ForCompetition(competitionIdValue),
                command.Sport,
                command.SeasonYear,
                command.CorrelationId,
                CausationId.Producer.EventCompetitionStatusDocumentProcessor
            ));
        }

        await _dataContext.CompetitionStatuses.AddAsync(entity);
        await _dataContext.SaveChangesAsync();

        _logger.LogInformation(
            "Persisted MLB CompetitionStatus. CompetitionId={CompId}, Status={Status}, HalfInning={HalfInning}, PeriodPrefix={PeriodPrefix}",
            competitionIdValue,
            entity.StatusTypeName,
            entity.HalfInning,
            entity.PeriodPrefix);
    }
}
