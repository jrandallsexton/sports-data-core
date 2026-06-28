using System;

using SportsData.Core.Common;

namespace SportsData.Core.Eventing.Events.Picks
{
    /// <summary>
    /// A user submitted (or changed) a pick for a contest. Published by the API
    /// when a pick is persisted, so the Notification service can project who has
    /// an active pick on a contest and target line-move / reminder notifications
    /// at actual pickers rather than all league members.
    ///
    /// <para>
    /// Idempotent projection target: the same (UserId, ContestId, PickemGroupId)
    /// may be republished whenever the user changes their pick or on
    /// at-least-once redelivery; the consumer upserts.
    /// </para>
    /// </summary>
    public record UserPickMade(
        Guid UserId,
        Guid ContestId,
        Guid PickemGroupId,
        Sport Sport,
        int? SeasonYear,
        Guid CorrelationId,
        Guid CausationId
    ) : EventBase(null, Sport, SeasonYear, CorrelationId, CausationId);
}
