using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Common;

/// <summary>
/// Intermediate base for the two sport-specific status-doc processors
/// (Football + Baseball). Owns the sport-agnostic lifecycle that fires
/// after the typed status entity has been built and the prior row (if
/// any) loaded:
///   1. Single Competition→Contest lookup for ContestId + SeasonWeekId.
///   2. ContestStatusChanged publish on a transition (existing → new).
///   3. ContestCompleted defensive publish on first observation of
///      STATUS_FINAL — closes the race where the streamer's publish
///      can fire before this processor persists the row, causing the
///      30s-deferred enrichment to short-circuit on the non-FINAL
///      CompetitionStatus row.
///   4. Contest.CancelledUtc stamp on first observation of
///      STATUS_CANCELED. Idempotent (preserves first-observed timestamp);
///      warns on reversal but treats cancellation as irrevocable.
///
/// Subclasses still own the parts that genuinely differ by sport:
/// DTO deserialization, entity construction (FootballCompetitionStatus
/// vs BaseballCompetitionStatus with its MLB-only fields and
/// FeaturedAthletes children), typed-DbSet load + hard-replace, and
/// the final AddAsync + SaveChangesAsync. They call
/// <see cref="HandleStatusLifecycleAsync"/> after the entity is built
/// and the prior status's StatusTypeName is in hand.
///
/// See docs/contest-enrichment-historical-sweep.md and the PR that
/// extracted this base.
/// </summary>
public abstract class EventCompetitionStatusProcessorBase<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : TeamSportDataContext
{
    protected readonly IDateTimeProvider _dateTimeProvider;

    protected EventCompetitionStatusProcessorBase(
        ILogger logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refGenerator,
        IDateTimeProvider dateTimeProvider)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refGenerator)
    {
        _dateTimeProvider = dateTimeProvider;
    }

    /// <summary>
    /// Runs the sport-agnostic status-doc lifecycle. Caller passes the
    /// new entity's status fields plus the prior row's StatusTypeName
    /// (null when this is the first observation of the competition's
    /// status). Caller is responsible for the AddAsync + SaveChangesAsync
    /// that flush this method's writes (CancelledUtc mutation on
    /// Contest) and the new status row.
    /// </summary>
    protected async Task HandleStatusLifecycleAsync(
        Guid competitionId,
        string newStatusTypeName,
        string newStatusDescription,
        string? existingStatusTypeName,
        ProcessDocumentCommand command)
    {
        // ContestStatusChanged fires only on a transition. First
        // observation (existingStatusTypeName is null) is NOT a
        // transition — there's no prior state to differ from. The new
        // status row is the first record of state, surfaced through
        // normal sourcing fan-out, not through a "status changed" event.
        var publishStatusChanged =
            existingStatusTypeName is not null && existingStatusTypeName != newStatusTypeName;

        var newStatusIsCanceled = newStatusTypeName == "STATUS_CANCELED";
        var existedAsCanceled = existingStatusTypeName == "STATUS_CANCELED";
        var newStatusIsFinal = newStatusTypeName == "STATUS_FINAL";
        var existedAsFinal = existingStatusTypeName == "STATUS_FINAL";
        var contestIdNeeded =
            publishStatusChanged || newStatusIsCanceled || existedAsCanceled || newStatusIsFinal;

        Guid contestId = Guid.Empty;
        Guid? seasonWeekId = null;
        if (contestIdNeeded)
        {
            // ContestId is what crosses the service boundary — Competition
            // is a Producer-internal sub-aggregate. SeasonWeekId comes
            // off Contest via the Competition→Contest navigation; EF
            // translates this to a join so we still avoid pulling full rows.
            var parent = await _dataContext.Competitions
                .Where(c => c.Id == competitionId)
                .Select(c => new { c.ContestId, SeasonWeekId = c.Contest!.SeasonWeekId })
                .FirstAsync();
            contestId = parent.ContestId;
            seasonWeekId = parent.SeasonWeekId;
        }

        if (publishStatusChanged)
        {
            _logger.LogInformation(
                "Contest status changed, publishing event. ContestId={ContestId}, CompetitionId={CompId}, OldStatus={OldStatus}, NewStatus={NewStatus}",
                contestId, competitionId, existingStatusTypeName, newStatusTypeName);

            await _publishEndpoint.Publish(new ContestStatusChanged(
                contestId,
                // Raw ESPN status type for programmatic branching
                // ("STATUS_FINAL") plus the human-readable description
                // ("Final") for display. Both come straight from the
                // status row — no transformation, no chance of drift.
                newStatusTypeName,
                newStatusDescription,
                _refGenerator.ForCompetition(competitionId),
                command.Sport,
                command.SeasonYear,
                command.CorrelationId,
                CausationId.Producer.EventCompetitionStatusDocumentProcessor
            ));
        }

        // Defensive ContestCompleted fan-out. The streamer publishes
        // this event at its STATUS_FINAL detection sites, but the
        // streamer's publish can race ahead of the status row write —
        // if the 30s-deferred enrichment runs before this processor
        // persists STATUS_FINAL, enrichment short-circuits on the
        // non-FINAL row and never re-fires. Publishing here, AFTER
        // the row is definitely written, closes that gap. Consumer is
        // idempotent (enrichment processor short-circuits on
        // Contest.FinalizedUtc != null), so a duplicate fire alongside
        // the streamer is harmless. No paired Event-document
        // re-source — Provider has dedupe but a defensive cascade
        // shouldn't risk a sourcing cycle.
        if (newStatusIsFinal && !existedAsFinal)
        {
            _logger.LogInformation(
                "Publishing ContestCompleted from status processor (defensive). ContestId={ContestId}, CompetitionId={CompId}",
                contestId, competitionId);

            await _publishEndpoint.Publish(new ContestCompleted(
                ContestId: contestId,
                CompetitionId: competitionId,
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
        // timestamp). Log a warning if ESPN reverses a cancellation
        // — treated as irrevocable. See
        // docs/contest-enrichment-historical-sweep.md.
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
                        contestId, newStatusTypeName);
                }
            }
        }
    }
}
