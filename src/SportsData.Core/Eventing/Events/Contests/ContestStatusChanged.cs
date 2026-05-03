using System;
using SportsData.Core.Common;

namespace SportsData.Core.Eventing.Events.Contests
{
    /// <summary>
    /// Sport-neutral lifecycle event. Published by Producer when a contest
    /// transitions Scheduled → InProgress → Final (or related lifecycle
    /// state changes such as Postponed). Carries no scoreboard fields —
    /// per-play tick data is split off to sport-specific
    /// <see cref="Football.FootballContestStateChanged"/> /
    /// <see cref="Baseball.BaseballContestStateChanged"/> events.
    /// </summary>
    public record ContestStatusChanged(
        Guid ContestId,
        string Status,
        Uri? Ref,
        Sport Sport,
        int? SeasonYear,
        Guid CorrelationId,
        Guid CausationId) : EventBase(Ref, Sport, SeasonYear, CorrelationId, CausationId);
}
