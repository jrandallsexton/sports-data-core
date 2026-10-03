using System;
using SportsData.Core.Common;

namespace SportsData.Core.Eventing.Events.Contests
{
    /// <summary>
    /// A contest's first odds arrived (football: one provider's row; baseball:
    /// the provider set from one wrapper document).
    /// </summary>
    /// <param name="DisplayedOdds">
    /// The displayed-row snapshot (see <see cref="DisplayedContestOdds"/>) when
    /// this event's odds include the row the matchup cards read; null
    /// otherwise, and null from pods publishing the prior shape during a
    /// rolling deploy. Consumers must treat null as "nothing to apply".
    /// </param>
    public record ContestOddsCreated(
        Guid ContestId,
        Uri? Ref,
        Sport Sport,
        int? SeasonYear,
        Guid CorrelationId,
        Guid CausationId,
        DisplayedContestOdds? DisplayedOdds = null
    ) : EventBase(Ref, Sport, SeasonYear, CorrelationId, CausationId);
}
