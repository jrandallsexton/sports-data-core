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
    private readonly IDateTimeProvider _dateTimeProvider;

    public EventCompetitionStatusDocumentProcessor(
        ILogger<EventCompetitionStatusDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refGenerator,
        IDateTimeProvider dateTimeProvider)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refGenerator)
    {
        _dateTimeProvider = dateTimeProvider;
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

        // ContestId is needed for ContestStatusChanged, ContestCompleted,
        // and the cancellation lifecycle update. Fetch once with SeasonWeekId
        // so the ContestCompleted payload doesn't require a second roundtrip.
        var newStatusIsCanceled = entity.StatusTypeName == "STATUS_CANCELED";
        var existedAsCanceled = existing?.StatusTypeName == "STATUS_CANCELED";
        var newStatusIsFinal = entity.StatusTypeName == "STATUS_FINAL";
        var existedAsFinal = existing?.StatusTypeName == "STATUS_FINAL";
        var contestIdNeeded =
            publishEvent || newStatusIsCanceled || existedAsCanceled || newStatusIsFinal;

        Guid contestId = Guid.Empty;
        Guid? seasonWeekId = null;
        if (contestIdNeeded)
        {
            // ContestId is what crosses the service boundary — Competition
            // is a Producer-internal sub-aggregate. SeasonWeekId comes off
            // Contest via the Competition→Contest navigation; EF translates
            // this to a join so we still avoid pulling full rows.
            var parent = await _dataContext.Competitions
                .Where(c => c.Id == competitionIdValue)
                .Select(c => new { c.ContestId, SeasonWeekId = c.Contest!.SeasonWeekId })
                .FirstAsync();
            contestId = parent.ContestId;
            seasonWeekId = parent.SeasonWeekId;
        }

        if (publishEvent)
        {
            _logger.LogInformation(
                "Contest status changed, publishing event. ContestId={ContestId}, CompetitionId={CompId}, NewStatus={Status}",
                contestId,
                competitionIdValue,
                entity.StatusTypeName);

            await _publishEndpoint.Publish(new ContestStatusChanged(
                contestId,
                // Raw ESPN status type for programmatic branching ("STATUS_FINAL"),
                // plus the human-readable description ("Final") for display.
                // Both come straight from the CompetitionStatus row — no
                // transformation, no chance of drift between code and DB.
                entity.StatusTypeName,
                entity.StatusDescription,
                _refGenerator.ForCompetition(competitionIdValue),
                command.Sport,
                command.SeasonYear,
                command.CorrelationId,
                CausationId.Producer.EventCompetitionStatusDocumentProcessor
            ));
        }

        // Defensive ContestCompleted fan-out. The streamer publishes this
        // event at its STATUS_FINAL detection sites, but the streamer's
        // ContestCompleted can race ahead of the status row write — if the
        // 30s-deferred enrichment runs before this processor persists
        // STATUS_FINAL, enrichment short-circuits on the non-FINAL status
        // and never re-fires. Publishing here, AFTER the status row is
        // definitely written, closes that gap. Consumer is idempotent
        // (enrichment processor short-circuits on Contest.FinalizedUtc !=
        // null), so a duplicate fire alongside the streamer is harmless.
        //
        // Skipping the paired Event-document re-source the streamer pairs
        // with its publish — Provider has dedupe but a defensive cascade
        // shouldn't risk a sourcing cycle.
        if (newStatusIsFinal && !existedAsFinal)
        {
            _logger.LogInformation(
                "Publishing ContestCompleted from status processor (defensive). ContestId={ContestId}, CompetitionId={CompId}",
                contestId, competitionIdValue);

            await _publishEndpoint.Publish(new ContestCompleted(
                ContestId: contestId,
                CompetitionId: competitionIdValue,
                SeasonWeekId: seasonWeekId,
                Ref: null,
                Sport: command.Sport,
                SeasonYear: command.SeasonYear,
                CorrelationId: command.CorrelationId,
                CausationId: CausationId.Producer.EventCompetitionStatusDocumentProcessor
            ));
        }

        // CancelledUtc lifecycle: stamp on first observation of
        // STATUS_CANCELED (idempotent — preserves "first observed"
        // timestamp). Log a warning if ESPN reverses a cancellation —
        // treated as irrevocable. See docs/contest-enrichment-historical-sweep.md.
        if (newStatusIsCanceled || existedAsCanceled)
        {
            var contest = await _dataContext.Contests.FindAsync(contestId);
            if (contest is not null)
            {
                if (newStatusIsCanceled && contest.CancelledUtc is null)
                {
                    contest.CancelledUtc = _dateTimeProvider.UtcNow();
                    _logger.LogInformation(
                        "Contest cancelled — stamped CancelledUtc. ContestId={ContestId}, CancelledUtc={CancelledUtc}",
                        contestId, contest.CancelledUtc);
                }
                else if (!newStatusIsCanceled && existedAsCanceled && contest.CancelledUtc is not null)
                {
                    _logger.LogWarning(
                        "Contest status moved away from STATUS_CANCELED but CancelledUtc is preserved (treated as irrevocable). ContestId={ContestId}, NewStatus={NewStatus}",
                        contestId, entity.StatusTypeName);
                }
            }
        }

        await _dataContext.Set<FootballCompetitionStatus>().AddAsync(entity);
        await _dataContext.SaveChangesAsync();

        _logger.LogInformation(
            "Persisted CompetitionStatus. CompetitionId={CompId}, Status={Status}",
            competitionIdValue,
            entity.StatusTypeName);
    }
}
