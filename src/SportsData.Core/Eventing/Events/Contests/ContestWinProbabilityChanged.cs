using System;
using SportsData.Core.Common;

namespace SportsData.Core.Eventing.Events.Contests
{
    /// <summary>
    /// Win-probability tick. Producer publishes when ESPN's per-competition
    /// probability snapshot moves. Sport-neutral — both football and
    /// baseball carry the same home/away/tie percentages.
    /// </summary>
    public record ContestWinProbabilityChanged(
        Guid ContestId,
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
