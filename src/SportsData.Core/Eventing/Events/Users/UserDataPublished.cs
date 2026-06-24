using System;

using SportsData.Core.Common;

namespace SportsData.Core.Eventing.Events.Users
{
    /// <summary>
    /// Per-user backfill snapshot emitted by the API in response to a
    /// <see cref="UsersRequested"/> trigger. Carries the fields the
    /// Notification service projects locally so it can render notification
    /// copy without an HTTP round-trip back to API.
    ///
    /// <para>
    /// One event per user. Consumer is responsible for idempotent upsert —
    /// at-least-once delivery means the same UserId may arrive twice, and
    /// repeated backfill requests will republish the entire set.
    /// </para>
    /// </summary>
    public record UserDataPublished(
        Guid UserId,
        string DisplayName,
        string Email,
        string Timezone,
        Sport Sport,
        int? SeasonYear,
        Guid CorrelationId,
        Guid CausationId
    ) : EventBase(null, Sport, SeasonYear, CorrelationId, CausationId);
}
