using System;

using SportsData.Core.Common;

namespace SportsData.Core.Eventing.Events.PickemGroups
{
    /// <summary>
    /// Backfill trigger asking the API to publish a
    /// <see cref="PickemGroupDataPublished"/> event for every league on file
    /// (with member roster bundled into each payload). Companion to
    /// <see cref="Users.UsersRequested"/> — same operator-triggered shape.
    /// </summary>
    public record PickemGroupsRequested(
        Sport Sport,
        int? SeasonYear,
        Guid CorrelationId,
        Guid CausationId
    ) : EventBase(null, Sport, SeasonYear, CorrelationId, CausationId);
}
