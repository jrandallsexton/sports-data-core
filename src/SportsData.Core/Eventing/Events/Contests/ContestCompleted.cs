using System;
using SportsData.Core.Common;

namespace SportsData.Core.Eventing.Events.Contests
{
    /// <summary>
    /// Sport-neutral lifecycle event. Published by Producer's
    /// <see cref="Application.Competitions.CompetitionStreamerBase{T}"/> at the
    /// three sites where it detects STATUS_FINAL:
    /// LiveStartOutcome.AlreadyFinal, the initial STATUS_FINAL switch arm,
    /// and PollOutcome.Final after the in-game polling loop exits.
    ///
    /// Carries enough context for the API-side scoring consumer to route
    /// without an extra DB lookup. The consumer is responsible for
    /// idempotency — at-least-once delivery means this event can re-deliver
    /// (e.g. admin replay of a completed game, broker redelivery, pod
    /// restart mid-broadcast). The downstream
    /// <see cref="Api.Application.Scoring.ContestScoringProcessor"/> already
    /// short-circuits when no UserPicks for the contest are unscored.
    ///
    /// Lifecycle status transitions (Scheduled → InProgress → Final) are
    /// also surfaced on <see cref="ContestStatusChanged"/>; ContestCompleted
    /// is a narrower, scoring-trigger-shaped event with the fields the
    /// scoring path needs.
    /// </summary>
    public record ContestCompleted(
        Guid ContestId,
        Guid CompetitionId,
        Guid? SeasonWeekId,
        Uri? Ref,
        Sport Sport,
        int? SeasonYear,
        Guid CorrelationId,
        Guid CausationId) : EventBase(Ref, Sport, SeasonYear, CorrelationId, CausationId);
}
