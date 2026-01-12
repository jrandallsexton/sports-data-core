using System;
using SportsData.Core.Common;

namespace SportsData.Core.Eventing.Events.Contests
{
    public record CompetitionPlayCompleted(
        Guid CompetitionPlayId,
        Guid CompetitionId,
        Guid ContestId,
        string PlayDescription,
        Uri? Ref,
        Sport Sport,
        int? SeasonYear,
        Guid CorrelationId,
        Guid CausationId) : EventBase(Ref, Sport, SeasonYear!, CorrelationId, CausationId);
}
