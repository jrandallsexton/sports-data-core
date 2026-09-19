using System;

using SportsData.Core.Common;

namespace SportsData.Core.Eventing.Events.Previews
{
    /// <summary>
    /// An operator approved a matchup preview. StatBot's pick for the contest
    /// follows the latest non-rejected preview, so approval is the moment the
    /// pick is re-derived in every league carrying the contest (it changes only
    /// when the approved preview names a different winner than the one already
    /// picked; a contest that has kicked off is never touched).
    /// </summary>
    public record MatchupPreviewApproved(
        Guid MatchupPreviewId,
        Guid ContestId,
        Uri? Ref,
        Sport Sport,
        int? SeasonYear,
        Guid CorrelationId,
        Guid CausationId
    ) : EventBase(Ref, Sport, SeasonYear, CorrelationId, CausationId);
}
