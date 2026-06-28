using System;

using SportsData.Core.Common;

namespace SportsData.Core.Eventing.Events.Users
{
    /// <summary>
    /// Emitted by the API when a mobile client unregisters its device —
    /// typically at sign-out — so the Notification service can drop the
    /// device's row from its local <c>UserDevices</c> table and stop sending
    /// the signed-out user's pushes to that device. The API resolves the
    /// authenticated Firebase token to the internal <see cref="UserId"/>
    /// before publishing; the device is identified by its stable
    /// <see cref="InstallationId"/>.
    ///
    /// <para>
    /// Consumer must be idempotent: deletion is scoped to
    /// <c>(InstallationId, UserId)</c> so a user only removes their own
    /// ownership of the device, and a missing row is a no-op.
    /// </para>
    /// </summary>
    public record UserDeviceUnregistered(
        Guid UserId,
        string InstallationId,
        Guid CorrelationId,
        Guid CausationId
    ) : EventBase(null, Sport.All, null, CorrelationId, CausationId);
}
