using System;

using SportsData.Core.Common;

namespace SportsData.Core.Eventing.Events.PickemGroups
{
    /// <summary>
    /// Backfill trigger asking the API to publish a
    /// <see cref="PickemGroupMatchupDataPublished"/> event for every future
    /// matchup on file (filter: <c>PickemGroupMatchup.StartDateUtc &gt; UtcNow</c>).
    /// Companion to <see cref="Users.UsersRequested"/> /
    /// <see cref="PickemGroupsRequested"/> — same operator-triggered shape.
    ///
    /// <para>
    /// Scope rationale: Notification only cares about contests that are
    /// actually picked in some league. Past matchups are excluded — picks
    /// have already locked and no reminder is meaningful. Filtering at the
    /// publisher keeps the projection small and skips events for events.
    /// </para>
    /// </summary>
    public record PickemGroupMatchupsRequested(
        Sport Sport,
        int? SeasonYear,
        Guid CorrelationId,
        Guid CausationId
    ) : EventBase(null, Sport, SeasonYear, CorrelationId, CausationId);
}
