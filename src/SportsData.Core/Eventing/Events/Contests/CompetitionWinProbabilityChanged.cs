using System;

namespace SportsData.Core.Eventing.Events.Contests
{
    public record CompetitionWinProbabilityChanged(
        Guid CompetitionId,
        Guid? PlayId,
        double HomeWinPercentage,
        double AwayWinPercentage,
        double TiePercentage,
        int SecondsLeft,
        DateTime EspnLastModifiedUtc,
        string Source,
        string Ref,
        string SequenceNumber,
        Guid CorrelationId,
        Guid CausationId
    ) : EventBase(CorrelationId, CausationId);
}