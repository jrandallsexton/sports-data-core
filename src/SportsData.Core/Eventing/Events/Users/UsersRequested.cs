using System;

using SportsData.Core.Common;

namespace SportsData.Core.Eventing.Events.Users
{
    /// <summary>
    /// Request issued by the Notification service (or any other service that
    /// needs to backfill its local <c>User</c> projection) asking the API to
    /// publish a <see cref="UserDataPublished"/> event for every user on file.
    ///
    /// <para>
    /// Not a "user created" or "user updated" event. This is a one-off backfill
    /// trigger — the API consumer iterates its <c>Users</c> table and emits
    /// one fat data event per row. Going forward, ongoing user lifecycle is
    /// expected to flow through separate steady-state events (TBD).
    /// </para>
    /// </summary>
    public record UsersRequested(
        Sport Sport,
        int? SeasonYear,
        Guid CorrelationId,
        Guid CausationId
    ) : EventBase(null, Sport, SeasonYear, CorrelationId, CausationId);
}
