using System;
using System.Collections.Generic;

using SportsData.Core.Common;

namespace SportsData.Core.Eventing.Events.Contests
{
    /// <summary>
    /// Instant-analysis facts for one live play ("T. Cooley's second
    /// interception of the season."). Published by Producer's analysis job
    /// after the play's fact set is persisted (PlayAnalysis rows), only for
    /// live analysis, never for replay-lab runs. Clients attach the items to
    /// the play with the same <see cref="PlayId"/> and drop them once a newer
    /// play arrives. Items are in display order (lowest priority first).
    /// See docs/events/contests/PlayAnalysisGenerated.md.
    /// </summary>
    public record PlayAnalysisGenerated(
        Guid ContestId,
        Guid CompetitionId,
        Guid PlayId,
        IReadOnlyList<PlayAnalysisItem> Items,
        Uri? Ref,
        Sport Sport,
        int? SeasonYear,
        Guid CorrelationId,
        Guid CausationId) : EventBase(Ref, Sport, SeasonYear, CorrelationId, CausationId);

    /// <summary>One fact: its PlayAnalysis row id, fact type and rendered sentence.</summary>
    public record PlayAnalysisItem(
        Guid Id,
        string FactType,
        string Text);
}
