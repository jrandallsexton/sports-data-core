using SportsData.Core.Common;

using System;

namespace SportsData.Core.Eventing.Events.PickemGroups
{
    /// <summary>
    /// Steady-state "this league now has this contest" signal for the
    /// Notification service's matchup projection.
    /// <see cref="Headline"/> ("Away Team at Home Team") feeds the
    /// single-unpicked-game reminder body; optional trailing parameter so
    /// pre-existing serialized messages deserialize with null.
    /// </summary>
    public record PickemGroupMatchupCreated(
        Guid GroupId,
        Guid ContestId,
        DateTime StartDateUtc,
        int SeasonWeek,
        Sport Sport,
        int? SeasonYear,
        Guid CorrelationId,
        Guid CausationId,
        string? Headline = null
    ) : EventBase(null, Sport, SeasonYear, CorrelationId, CausationId);
}
