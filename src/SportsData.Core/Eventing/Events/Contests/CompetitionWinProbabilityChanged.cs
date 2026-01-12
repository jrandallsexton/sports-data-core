using System;
using SportsData.Core.Common;

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
        string SourceRef,
        string SequenceNumber,
        Uri? Ref,
        Sport Sport,
        int? SeasonYear,
        Guid CorrelationId,
        Guid CausationId
    ) : EventBase(Ref, Sport, SeasonYear, CorrelationId, CausationId);
}