using System;
using SportsData.Core.Common;

namespace SportsData.Core.Eventing.Events.Contests
{
    /// <summary>
    /// Published by Producer's sport-specific ContestEnrichmentProcessor after
    /// the canonical Contest row has been enriched with final scores, winner,
    /// odds results, and FinalizedUtc. This is the trigger API consumes to
    /// kick off picks scoring — distinct from <see cref="ContestCompleted"/>,
    /// which fires the moment STATUS_FINAL is detected and predates the
    /// enrichment write.
    /// </summary>
    public record ContestFinalized(
        Guid ContestId,
        Uri? Ref,
        Sport Sport,
        int? SeasonYear,
        Guid CorrelationId,
        Guid CausationId
        ) : EventBase(Ref, Sport, SeasonYear, CorrelationId, CausationId);
}
