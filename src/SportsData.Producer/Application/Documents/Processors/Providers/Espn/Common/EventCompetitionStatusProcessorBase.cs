using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Infrastructure.DataSources.Espn;
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

        // Defensive ContestCompleted fan-out + targeted per-competitor
        // score-doc re-source. The streamer publishes ContestCompleted at
        // its STATUS_FINAL detection sites, but the streamer's publish can
        // race ahead of the status row write — if the 30s-deferred
        // enrichment runs before this processor persists STATUS_FINAL,
        // enrichment short-circuits on the non-FINAL row and never re-fires.
        // Publishing here, AFTER the row is definitely written, closes that
        // gap. Consumer is idempotent (enrichment processor short-circuits
        // on Contest.FinalizedUtc != null), so a duplicate fire alongside
        // the streamer is harmless.
        //
        // Score-doc re-source: enrichment reads MAX(CompetitionCompetitor-
        // Scores.Value), so if ESPN's feed hasn't propagated the deciding
        // inning into CCS by the time enrichment runs, we finalize with
        // stale scores (the 2026-06-20 White Sox @ Tigers 2-2 corruption
        // shape, repaired by the audit job but better prevented). Publish a
        // targeted DocumentRequested per competitor score URI here so
        // Provider re-fetches from ESPN immediately. Provider's
        // ShouldBypassCache returns true for current-season requests, and
        // the content-hash dedupe in ResourceIndexItemProcessor naturally
        // suppresses the downstream publish if the score actually hasn't
        // changed — so the worst case is two ESPN fetches per game-end
        // (negligible against the rate-limit envelope). Restricted to the
        // score docs only — NOT a re-source of the Event doc — to avoid a
        // status → event → status sourcing cycle.
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

            await RequestCompetitorScoreResourceAsync(competitionId, command);
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

    /// <summary>
    /// On STATUS_FINAL transition, publish a DocumentRequested for each
    /// per-competitor score URI so Provider re-fetches from ESPN before the
    /// 30s-deferred enrichment runs. Without this, enrichment frequently
    /// reads stale CCS values (the corruption shape PR #431 + the broader
    /// MLB-tie guard ship after-the-fact, but better prevented).
    ///
    /// Loads CompetitionCompetitor.ExternalIds filtered to the Espn
    /// provider — one round-trip — and derives the score URI from each
    /// competitor's ref via EspnUriMapper. If a competitor has no Espn
    /// external id (shouldn't happen for ESPN-sourced games but worth
    /// guarding), it's skipped with a warning.
    /// </summary>
    private async Task RequestCompetitorScoreResourceAsync(
        Guid competitionId,
        ProcessDocumentCommand command)
    {
        var competitorRefs = await _dataContext.CompetitionCompetitors
            .AsNoTracking()
            .Where(cc => cc.CompetitionId == competitionId)
            .Select(cc => new
            {
                cc.Id,
                EspnRef = cc.ExternalIds
                    .Where(x => x.Provider == SourceDataProvider.Espn)
                    .Select(x => x.SourceUrl)
                    .FirstOrDefault()
            })
            .ToListAsync();

        foreach (var competitor in competitorRefs)
        {
            if (string.IsNullOrWhiteSpace(competitor.EspnRef))
            {
                _logger.LogWarning(
                    "On-final score-doc re-source skipped — no Espn external id. CompetitionId={CompId}, CompetitorId={CompetitorId}",
                    competitionId, competitor.Id);
                continue;
            }

            // TryCreate over `new Uri(...)` so a malformed stored ref
            // doesn't tank the fan-out for the OTHER competitor in this
            // loop (or bubble up and kill the surrounding document
            // processor pass). Malformed URIs in the external-id store
            // are non-retryable contract violations — log loudly so the
            // bad row is fixable, skip individually, and let the other
            // competitor's request go through.
            if (!Uri.TryCreate(competitor.EspnRef, UriKind.Absolute, out var competitorRef))
            {
                _logger.LogError(
                    "On-final score-doc re-source skipped — competitor external ref is not a valid absolute URI. CompetitionId={CompId}, CompetitorId={CompetitorId}, EspnRef={EspnRef}",
                    competitionId, competitor.Id, competitor.EspnRef);
                continue;
            }

            // Same per-competitor fault-isolation reasoning as the
            // TryCreate guard above. The URI parses, but ESPN's expected
            // /events/{id}/competitions/{id}/competitors/{id} shape might
            // not hold if the stored ref was corrupted or pointed at the
            // wrong resource — BuildCompetitionCompetitorRefFrom throws
            // ArgumentException in that case. Log + skip so the other
            // competitor's publish still goes out.
            Uri scoreRef;
            try
            {
                scoreRef = EspnUriMapper.CompetitionCompetitorRefToCompetitionCompetitorScoreRef(competitorRef);
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(
                    ex,
                    "On-final score-doc re-source skipped — competitor external ref did not match the expected ESPN shape. CompetitionId={CompId}, CompetitorId={CompetitorId}, EspnRef={EspnRef}",
                    competitionId, competitor.Id, competitor.EspnRef);
                continue;
            }

            _logger.LogInformation(
                "Publishing on-final score-doc re-source. CompetitionId={CompId}, CompetitorId={CompetitorId}, ScoreUri={ScoreUri}",
                competitionId, competitor.Id, scoreRef);

            await _publishEndpoint.Publish(new DocumentRequested(
                Id: Guid.NewGuid().ToString(),
                ParentId: competitor.Id.ToString(),
                Uri: scoreRef,
                Ref: null,
                Sport: command.Sport,
                SeasonYear: command.SeasonYear,
                DocumentType: DocumentType.EventCompetitionCompetitorScore,
                SourceDataProvider: SourceDataProvider.Espn,
                CorrelationId: command.CorrelationId,
                CausationId: CausationId.Producer.EventCompetitionStatusDocumentProcessor
            ));
        }
    }
}
