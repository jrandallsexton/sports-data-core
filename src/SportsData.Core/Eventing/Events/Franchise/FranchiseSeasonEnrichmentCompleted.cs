using System;
using System.Collections.Generic;

using SportsData.Core.Common;

namespace SportsData.Core.Eventing.Events.Franchise
{
    /// <param name="ContestIds">
    /// Every contest the franchise season plays, any status. Lets a consumer
    /// find the league matchups that show this team's record without a
    /// Producer round trip (the API cannot map a franchise season to
    /// contests on its own). Null from a publisher that predates the field.
    /// </param>
    public record FranchiseSeasonEnrichmentCompleted(
        Guid FranchiseSeasonId,
        Uri? Ref,
        Sport Sport,
        int? SeasonYear,
        Guid CorrelationId,
        Guid CausationId,
        IReadOnlyList<Guid>? ContestIds = null
    ) : EventBase(Ref, Sport, SeasonYear, CorrelationId, CausationId);
}
