using System;
using SportsData.Core.Common;

namespace SportsData.Core.Eventing.Events.Contests
{
    /// <summary>
    /// Sport-neutral lifecycle event. Published by Producer when a contest
    /// transitions Scheduled → InProgress → Final (or related lifecycle
    /// state changes such as Postponed). Carries no scoreboard or per-play
    /// fields — per-play data is split off to sport-specific
    /// <see cref="Football.FootballPlayCompleted"/> /
    /// <see cref="Baseball.BaseballPlayCompleted"/> events.
    /// </summary>
    /// <param name="Status">
    /// Raw ESPN status type name — "STATUS_IN_PROGRESS", "STATUS_FINAL",
    /// "STATUS_RAIN_DELAY". Stable identifier for programmatic branching.
    /// Sourced from <c>CompetitionStatus.StatusTypeName</c>.
    /// </param>
    /// <param name="StatusDescription">
    /// Human-readable status — "In Progress", "Final", "Rain Delay". For
    /// display. Sourced from <c>CompetitionStatus.StatusDescription</c>.
    /// </param>
    public record ContestStatusChanged(
        Guid ContestId,
        string Status,
        string StatusDescription,
        Uri? Ref,
        Sport Sport,
        int? SeasonYear,
        Guid CorrelationId,
        Guid CausationId) : EventBase(Ref, Sport, SeasonYear, CorrelationId, CausationId);
}
