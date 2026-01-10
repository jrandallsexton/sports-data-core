using System;
using SportsData.Core.Common;

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
        bool IsScoringPlay,
        Uri? Ref,
        Sport Sport,
        int? SeasonYear,
        Guid CorrelationId,
        Guid CausationId) : EventBase(Ref, Sport, SeasonYear, CorrelationId, CausationId);
}
