using System;

using SportsData.Core.Common;

namespace SportsData.Core.Eventing.Events.Users
{
    /// <summary>
    /// Emitted by the API when a user deletes their account. The canonical
    /// <c>User</c> is anonymized (PII stripped, login removed) but game history
    /// is retained; this event tells the Notification service to purge the
    /// user's downstream footprint — devices, notification preferences, its
    /// <c>User</c> projection, scheduled jobs, and logs — so all push and
    /// reminders stop.
    ///
    /// <para>
    /// Consumer must be idempotent: purge is by <see cref="UserId"/> and a
    /// missing row is a no-op.
    /// </para>
    /// </summary>
    public record UserDeleted(
        Guid UserId,
        Guid CorrelationId,
        Guid CausationId
    ) : EventBase(null, Sport.All, null, CorrelationId, CausationId);
}
