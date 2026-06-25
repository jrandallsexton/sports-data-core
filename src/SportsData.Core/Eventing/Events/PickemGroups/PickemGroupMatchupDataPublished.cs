using System;

using SportsData.Core.Common;

namespace SportsData.Core.Eventing.Events.PickemGroups
{
    /// <summary>
    /// Per-matchup backfill snapshot. Carries the fields Notification needs
    /// to schedule pick-deadline + kickoff reminders without re-querying API:
    /// the league this matchup belongs to, the contest, and the kickoff
    /// time. Status is intentionally NOT on the payload — API doesn't store
    /// canonical contest status. Notification defaults it to
    /// <c>"STATUS_SCHEDULED"</c> on insert and updates it via steady-state
    /// <c>ContestStatusChanged</c> consumption (Phase 2c-main / 2d).
    ///
    /// <para>
    /// One event per matchup. Consumer is responsible for idempotent upsert.
    /// At-least-once delivery means the same (PickemGroupId, ContestId) may
    /// arrive twice; repeated backfill requests will republish the entire
    /// set.
    /// </para>
    /// </summary>
    public record PickemGroupMatchupDataPublished(
        Guid PickemGroupId,
        Guid ContestId,
        DateTime StartDateUtc,
        int SeasonWeek,
        Sport Sport,
        int? SeasonYear,
        Guid CorrelationId,
        Guid CausationId
    ) : EventBase(null, Sport, SeasonYear, CorrelationId, CausationId);
}
