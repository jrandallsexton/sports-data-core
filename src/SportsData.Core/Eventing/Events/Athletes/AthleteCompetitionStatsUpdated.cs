using System;

using SportsData.Core.Common;

namespace SportsData.Core.Eventing.Events.Athletes
{
    /// <summary>
    /// An athlete's box-score statline for one competition was written
    /// (created or replaced) by the stat document processor. The precise
    /// trigger for Player Pick'em score persistence: the API consumer
    /// matches PlayerLineupSlot anchors on (ContestId, AthleteSeasonId)
    /// and recomputes exactly the affected slots — no polling, no jobs.
    /// Requires a per-sport RabbitMQ shovel to reach the API broker (see
    /// sports-data-config shovels/README.md).
    /// </summary>
    public record AthleteCompetitionStatsUpdated(
        Guid ContestId,
        Guid CompetitionId,
        Guid AthleteSeasonId,
        Uri? Ref,
        Sport Sport,
        int? SeasonYear,
        Guid CorrelationId,
        Guid CausationId) : EventBase(Ref, Sport, SeasonYear, CorrelationId, CausationId);
}
