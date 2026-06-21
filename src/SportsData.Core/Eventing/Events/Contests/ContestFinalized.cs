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
    ///
    /// Carries the enriched result fields so API can broadcast them over
    /// SignalR without round-tripping back to Producer. Web clients then
    /// merge these into their live-update cache to flip the matchup card
    /// from "raw STATUS_FINAL" (no cover line, no SU checkmark) to fully
    /// enriched without a page refresh.
    ///
    /// All result fields are nullable because:
    ///   - <see cref="WinnerFranchiseSeasonId"/> is null on a tie (rare in
    ///     football, impossible in MLB per the tie guards).
    ///   - <see cref="SpreadWinnerFranchiseSeasonId"/> is null on a true
    ///     spread push (game landed exactly on the line).
    ///   - <see cref="OverUnderResultRaw"/> is null when no odds were
    ///     enriched. Carrier wire type is int? mapping to the Producer-side
    ///     OverUnderResult enum (None=0, Over=1, Under=2, Push=3). Sent as
    ///     int? so Core stays free of the Producer-side enum; consumers
    ///     translate to the appropriate display type.
    ///   - <see cref="AwayScore"/> / <see cref="HomeScore"/> /
    ///     <see cref="CompletedUtc"/> are always populated by enrichment
    ///     before publish, but kept nullable so older Producer pods
    ///     publishing the prior shape don't fail deserialization during a
    ///     rolling deploy.
    /// </summary>
    public record ContestFinalized(
        Guid ContestId,
        Uri? Ref,
        Sport Sport,
        int? SeasonYear,
        Guid CorrelationId,
        Guid CausationId,
        int? AwayScore = null,
        int? HomeScore = null,
        Guid? WinnerFranchiseSeasonId = null,
        Guid? SpreadWinnerFranchiseSeasonId = null,
        int? OverUnderResultRaw = null,
        DateTime? CompletedUtc = null
        ) : EventBase(Ref, Sport, SeasonYear, CorrelationId, CausationId);
}
