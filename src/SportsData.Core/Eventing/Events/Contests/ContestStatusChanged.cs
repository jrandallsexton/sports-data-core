using System;

namespace SportsData.Core.Eventing.Events.Contests
{
    public record ContestStatusChanged(
        Guid ContestId,
        string Status,
        string Period,
        string Clock,
        int AwayScore,
        int HomeScore,
        Guid? PossessionFranchiseSeasonId,
        Guid CorrelationId,
        Guid CausationId) : EventBase(CorrelationId, CausationId);
}
